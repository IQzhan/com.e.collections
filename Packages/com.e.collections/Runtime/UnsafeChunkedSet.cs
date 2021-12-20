using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    /// <summary>
    /// Red-Black-Tree
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    public unsafe struct UnsafeChunkedSet<Key> : ICollection, IChunked, IResizeable
        where Key : unmanaged, IComparable<Key>, IComparable
    {
        private struct Head
        {
            public Allocator allocator;
            public int preSize;
            public int valueSize;
            public int lockedMark;
            public Node* root;
            public UnsafeChunkedList data;
        }

        private enum Color
        {
            Black = 0,
            Red = 1
        }

        private struct Node
        {
            public int index;
            public Color color;
            public Node* left;
            public Node* right;
            public Node* parent;
            public Key key;
        }

        private Head* m_Head;

        public bool IsCreated => m_Head != null;

        public int Count
        {
            get
            {
                CheckExists();
                return m_Head->data.Count;
            }
        }

        public long ChunkSize
        {
            get
            {
                CheckExists();
                return m_Head->data.ChunkSize;
            }
        }

        public int ChunkCount
        {
            get
            {
                CheckExists();
                return m_Head->data.ChunkCount;
            }
        }

        public int ElementSize
        {
            get
            {
                CheckExists();
                return m_Head->data.ElementSize;
            }
        }

        /// <summary>
        /// Create a red-black tree.
        /// </summary>
        /// <param name="valueSize"></param>
        /// <param name="chunkSize"></param>
        /// <param name="allocator"></param>
        public UnsafeChunkedSet(int valueSize, long chunkSize, Allocator allocator)
        {
            int preSize = Memory.SizeOf<Node>();
            int nodeSize = preSize + valueSize;
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                allocator = allocator,
                preSize = preSize,
                valueSize = valueSize,
                lockedMark = 0,
                root = null,
                data = new UnsafeChunkedList(nodeSize, chunkSize, allocator)
            };
        }

        /// <summary>
        /// Get no thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* this[int index]
        {
            get
            {
                CheckExists();
                return m_Head->data[index] + m_Head->preSize;
            }
        }

        /// <summary>
        /// Check key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(Key key)
        {
            return TryGetNode(key, out var _);
        }

        /// <summary>
        /// Get index if key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int IndexOf(Key key)
        {
            if (TryGetNode(key, out Node* node))
            {
                return node->index;
            }
            return -1;
        }

        /// <summary>
        /// Get thread safe.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(Key key, out byte* value)
        {
            if (TryGetNode(key, out Node* node))
            {
                value = (byte*)node + m_Head->preSize;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Get by index thread safe.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* GetByIndex(int index)
        {
            CheckExists();
            Lock();
            byte* result = m_Head->data[index] + m_Head->preSize;
            Unlock();
            return result;
        }

        private bool TryGetNode(Key key, out Node* node)
        {
            CheckExists();
            Lock();
            node = m_Head->root;
            while (node != null)
            {
                int compare = key.CompareTo(node->key);
                if (compare < 0)
                {
                    node = node->left;
                }
                else if (compare > 0)
                {
                    node = node->right;
                }
                else
                {
                    Unlock();
                    return true;
                }
            }
            Unlock();
            return false;
        }

        /// <summary>
        /// Set thread safe.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public byte* Set(Key key)
        {
            CheckExists();
            Lock();
            Node* node, parent;
            node = m_Head->root;
            parent = node;
            byte* res;
            while (node != null)
            {
                parent = node;
                int compare = key.CompareTo(node->key);
                if (compare < 0)
                {
                    node = node->left;
                }
                else if (compare > 0)
                {
                    node = node->right;
                }
                else
                {
                    res = (byte*)node + m_Head->preSize;
                    Unlock();
                    return res;
                }
            }
            //��ʼ����ɫ�ڵ�
            node = Malloc();
            node->key = key;
            node->color = Color.Red;
            node->left = node->right = null;
            node->parent = parent;
            res = (byte*)node + m_Head->preSize;
            //�븸����
            if (m_Head->root == null)
            {
                m_Head->root = node;
            }
            else if (key.CompareTo(parent->key) < 0)
            {
                parent->left = node;
            }
            else
            {
                parent->right = node;
            }
            //����ͨ����������Ϊ�����
            AddFixup(node);
            Unlock();
            return res;
        }

        /// <summary>
        /// Remove thread safe.
        /// </summary>
        /// <param name="key"></param>
        public void Remove(Key key)
        {
            CheckExists();
            Lock();
            if (m_Head->root == null)
            {
                Unlock();
                return;
            }
            Node* toDelete = m_Head->root;
            while (toDelete != null)
            {
                int compare = key.CompareTo(toDelete->key);
                if (compare < 0)
                {
                    toDelete = toDelete->left;
                }
                else if (compare > 0)
                {
                    toDelete = toDelete->right;
                }
                else
                {
                    break;
                }
            }
            if (toDelete == null)
            {
                Unlock();
                return;
            }
            Node* forReplace = toDelete;
            // ��������ӽڵ㣬���ҵ�����������С�Ľ�㣬��֮����
            if (toDelete->left != null && toDelete->right != null)
            {
                forReplace = FindMin(toDelete->right);
                // ����ֻ��ֵ���и��ƣ�����������ɫ�������ƻ������������
                toDelete->key = forReplace->key;
                UnsafeUtility.MemCpy((byte*)toDelete + m_Head->preSize, (byte*)forReplace + m_Head->preSize, m_Head->valueSize);
                // ����Ǻ�ɫ��Ϊĩβ������Ǻ�ɫ�򻹿�����һ�����ӽڵ㣬������滻
                toDelete = forReplace;
            }
            bool hasLeft = toDelete->left != null;
            bool hasRight = toDelete->right != null;
            if (!hasLeft && hasRight)
            {
                // �ڵ�Ϊ��,��ֻ�к����ӽڵ�
                forReplace = toDelete->right;
            }
            else if (hasLeft && !hasRight)
            {
                // �ڵ�Ϊ�ڣ���ֻ�к����ӽڵ�
                forReplace = toDelete->left;
            }
            // ����forReplace��toDelete
            if (forReplace != toDelete)
            {
                forReplace->parent = toDelete->parent;
                forReplace->color = Color.Black;
                toDelete->color = Color.Red;
                if (toDelete->parent != null)
                {
                    if (toDelete == toDelete->parent->left)
                    {
                        toDelete->parent->left = forReplace;
                    }
                    else
                    {
                        toDelete->parent->right = forReplace;
                    }
                    toDelete->parent = null;
                }
                else
                {
                    m_Head->root = forReplace;
                }
            }
            // ���������
            RemoveFixup(toDelete);
            // �ͷ�
            Free(toDelete);
            Unlock();
        }

        /// <summary>
        /// Extend thread safe.
        /// </summary>
        /// <param name="count"></param>
        public void Extend(int count)
        {
            CheckExists();
            Lock();
            m_Head->data.Extend(count);
            Unlock();
        }

        /// <summary>
        /// Clear all thread safe.
        /// </summary>
        public void Clear()
        {
            CheckExists();
            Lock();
            m_Head->data.Clear();
            m_Head->root = null;
            Unlock();
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            CheckExists();
            m_Head->data.Dispose();
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        private void AddFixup(Node* inserted)
        {
            if (inserted == m_Head->root)
            {
                inserted->color = Color.Black;
                return;
            }
            // ��Ϊ��ɫʱ����Ҫ����
            while (inserted->parent != null && inserted->parent->color == Color.Red)
            {
                // ��������游��������
                if (inserted->parent == inserted->parent->parent->left)
                {
                    Node* uncle = inserted->parent->parent->right;
                    // ���
                    if (uncle != null && uncle->color == Color.Red)
                    {
                        inserted->parent->color = Color.Black;
                        uncle->color = Color.Black;
                        inserted->parent->parent->color = Color.Red;
                        inserted = inserted->parent->parent;
                    }
                    // ���
                    else
                    {
                        // ��ǰ����Ǹ���������
                        if (inserted == inserted->parent->right)
                        {
                            inserted = inserted->parent;
                            LeftRotate(inserted);
                        }
                        // ��ǰ����Ǹ���������
                        else
                        {
                            inserted->parent->color = Color.Black;
                            inserted->parent->parent->color = Color.Red;
                            RightRotate(inserted->parent->parent);
                        }
                    }
                }
                // ��������游��������
                else
                {
                    Node* uncle = inserted->parent->parent->left;
                    // ���
                    if (uncle != null && uncle->color == Color.Red)
                    {
                        inserted->parent->color = Color.Black;
                        uncle->color = Color.Black;
                        inserted->parent->parent->color = Color.Red;
                        inserted = inserted->parent->parent;
                    }
                    // ���
                    else
                    {
                        // ��ǰ����Ǹ���������
                        if (inserted == inserted->parent->left)
                        {
                            inserted = inserted->parent;
                            RightRotate(inserted);
                        }
                        // ��ǰ����Ǹ���������
                        else
                        {
                            inserted->parent->color = Color.Black;
                            inserted->parent->parent->color = Color.Red;
                            LeftRotate(inserted->parent->parent);
                        }
                    }
                }
            }
            // ��Ϊ��
            m_Head->root->color = Color.Black;
        }

        private void RemoveFixup(Node* node)
        {
            if (node->color == Color.Red || node == m_Head->root)
            {
                return;
            }
            while (node != m_Head->root)
            {
                // node�Ǻ�ɫ���ӽڵ������ֵܣ���ʱ��װnode == null
                if (node == node->parent->right)
                // ������
                {
                    Node* bro = node->parent->left;
                    if (bro->color == Color.Black)
                    {
                        // �ֺ�
                        bool broLBlack = bro->left == null || (bro->left->color == Color.Black);
                        bool broRBlack = bro->right == null || (bro->right->color == Color.Black);
                        // ����ȫ��
                        if (broLBlack && broRBlack)
                        {
                            // ����
                            if (node->parent->color == Color.Black)
                            {
                                bro->color = Color.Red;
                                node = node->parent;
                            }
                            // ����
                            else
                            {
                                bro->color = Color.Red;
                                node->parent->color = Color.Black;
                                break;
                            }
                        }
                        // ���Ӳ�ȫ��
                        else
                        {
                            // �����Ӻ�
                            if (broLBlack)
                            {
                                bro->color = Color.Red;
                                bro->right->color = Color.Black;
                                LeftRotate(bro);
                            }
                            // �����Ӻ�
                            else
                            {
                                Color c = node->parent->color;
                                node->parent->color = bro->color;
                                bro->color = c;
                                bro->left->color = Color.Black;
                                RightRotate(node->parent);
                                break;
                            }
                        }
                    }
                    // �ֺ죬����һ�����ӽڵ�
                    else
                    {
                        RightRotate(node->parent);
                        node->parent->color = Color.Red;
                        bro->color = Color.Black;
                    }
                }
                // ������
                else
                {
                    Node* bro = node->parent->right;
                    // �ֺ�
                    if (bro->color == Color.Black)
                    {
                        bool broLBlack = bro->left == null || (bro->left->color == Color.Black);
                        bool broRBlack = bro->right == null || (bro->right->color == Color.Black);
                        // ����ȫ��
                        if (broLBlack && broRBlack)
                        {
                            // ����
                            if (node->parent->color == Color.Black)
                            {
                                bro->color = Color.Red;
                                node = node->parent;
                            }
                            // ����
                            else
                            {
                                bro->color = Color.Red;
                                node->parent->color = Color.Black;
                                break;
                            }
                        }
                        // ���Ӳ�ȫ��
                        else
                        {
                            // �����Ӻ�
                            if (broRBlack)
                            {
                                bro->color = Color.Red;
                                bro->left->color = Color.Black;
                                RightRotate(bro);
                            }
                            // �����Ӻ�
                            else
                            {
                                Color c = node->parent->color;
                                node->parent->color = bro->color;
                                bro->color = c;
                                bro->right->color = Color.Black;
                                LeftRotate(node->parent);
                                break;
                            }
                        }
                    }
                    // �ֺ죬����һ�����ӽڵ�
                    else
                    {
                        LeftRotate(node->parent);
                        node->parent->color = Color.Red;
                        bro->color = Color.Black;
                    }
                }
            }
            // ���ڵ�����Ǻ�
            m_Head->root->color = Color.Black;
        }

        private void LeftRotate(Node* node)
        {
            if (node == null)
            {
                return;
            }
            Node* nodeR = node->right;
            if (nodeR == null)
            {
                return;
            }
            node->right = nodeR->left;
            if (node->right != null)
            {
                node->right->parent = node;
            }
            Node* nodeP = node->parent;
            nodeR->parent = nodeP;
            if (nodeP == null)
            {
                m_Head->root = nodeR;
            }
            else if (nodeP->left == node)
            {
                nodeP->left = nodeR;
            }
            else
            {
                nodeP->right = nodeR;
            }
            nodeR->left = node;
            node->parent = nodeR;
        }

        private void RightRotate(Node* node)
        {
            if (node == null)
            {
                return;
            }
            Node* nodeL = node->left;
            if (nodeL == null)
            {
                return;
            }
            node->left = nodeL->right;
            if (null != node->left)
            {
                node->left->parent = node;
            }
            Node* nodeP = node->parent;
            nodeL->parent = nodeP;
            if (nodeP == null)
            {
                m_Head->root = nodeL;
            }
            else if (nodeP->left == node)
            {
                nodeP->left = nodeL;
            }
            else
            {
                nodeP->right = nodeL;
            }
            nodeL->right = node;
            node->parent = nodeL;
        }

        private Node* FindMin(Node* t)
        {
            if (t == null)
            {
                return null;
            }
            while (t->left != null)
            {
                t = t->left;
            }
            return t;
        }

        /// <summary>
        /// �����ַ
        /// </summary>
        /// <returns></returns>
        private Node* Malloc()
        {
            int index = m_Head->data.Count;
            Node* ptr = (Node*)m_Head->data.Add();
            ptr->index = index;
            return ptr;
        }

        /// <summary>
        /// �ͷŵ�ַ
        /// </summary>
        /// <param name="node"></param>
        private void Free(Node* node)
        {
            // ���������
            if (node == m_Head->root)
            {
                m_Head->root = null;
            }
            else if (node->parent != null)
            {
                if (node->parent->left == node)
                {
                    node->parent->left = null;
                }
                else if (node->parent->right == node)
                {
                    node->parent->right = null;
                }
            }
            int index = node->index;
            // �����һ��element
            if (index == m_Head->data.Count - 1)
            {
                // ֱ���Ƴ�
                m_Head->data.RemoveLast();
            }
            // �������һ��element
            else
            {
                // �����һ��element�ƶ���node
                m_Head->data.SwapLastAndRemove(index);
                // �±�����Ϊ��ǰnode�±�
                node->index = index;
                // ��������element��ַ��Ϊnode����Ҫ�������Ӹ��ӽ��
                // �Ǹ��ڵ�
                if (node->parent == null)
                {
                    m_Head->root = node;
                }
                // �Ǹ��ڵ�����ӽڵ�
                else if (node->key.CompareTo(node->parent->key) < 0)
                {
                    node->parent->left = node;
                }
                // �Ǹ��ڵ�����ӽڵ�
                else
                {
                    node->parent->right = node;
                }
                // �������ӽڵ�
                if (node->left != null)
                {
                    node->left->parent = node;
                }
                if (node->right != null)
                {
                    node->right->parent = node;
                }
            }
        }

        private void Lock()
        {
            while (1 == Interlocked.Exchange(ref m_Head->lockedMark, 1)) ;
        }

        private void Unlock()
        {
            Interlocked.Exchange(ref m_Head->lockedMark, 0);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null)
            {
                throw new NullReferenceException($"{nameof(UnsafeChunkedSet<Key>)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void Check()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckExists();
            int pathBlackCount = GetPathBlackCount(m_Head->root);
            if (pathBlackCount == 0)
            {
                throw new Exception("This is not a red-black-tree.");
            }
#endif
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private int GetPathBlackCount(Node* node)
        {
            int leftCount = 0, rightCount = 0;
            if (node != null)
            {
                leftCount = GetPathBlackCount(node->left);
                rightCount = GetPathBlackCount(node->right);
            }
            if (node != null && node->color == Color.Red)
            {
                bool leftBlack = node->left == null || node->left->color == Color.Black;
                bool rightBlack = node->right == null || node->right->color == Color.Black;
                return (leftBlack && rightBlack && (leftCount == rightCount)) ? leftCount : 0;
            }
            else
            {
                return (leftCount == rightCount ? leftCount : 0) + 1;
            }
        }
#endif
    }
}