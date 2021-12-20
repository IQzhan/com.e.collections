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
            //初始化红色节点
            node = Malloc();
            node->key = key;
            node->color = Color.Red;
            node->left = node->right = null;
            node->parent = parent;
            res = (byte*)node + m_Head->preSize;
            //与父连接
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
            //将普通二叉树修正为红黑树
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
            // 如果两个子节点，就找到右子树中最小的结点，将之代替
            if (toDelete->left != null && toDelete->right != null)
            {
                forReplace = FindMin(toDelete->right);
                // 这里只对值进行复制，并不复制颜色，以免破坏红黑树的性质
                toDelete->key = forReplace->key;
                UnsafeUtility.MemCpy((byte*)toDelete + m_Head->preSize, (byte*)forReplace + m_Head->preSize, m_Head->valueSize);
                // 如果是红色则为末尾，如果是黑色则还可能有一个红子节点，需继续替换
                toDelete = forReplace;
            }
            bool hasLeft = toDelete->left != null;
            bool hasRight = toDelete->right != null;
            if (!hasLeft && hasRight)
            {
                // 节点为黑,且只有红右子节点
                forReplace = toDelete->right;
            }
            else if (hasLeft && !hasRight)
            {
                // 节点为黑，且只有红左子节点
                forReplace = toDelete->left;
            }
            // 交换forReplace和toDelete
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
            // 修正红黑树
            RemoveFixup(toDelete);
            // 释放
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
            // 父为红色时才需要调整
            while (inserted->parent != null && inserted->parent->color == Color.Red)
            {
                // 父结点是祖父结点的左子
                if (inserted->parent == inserted->parent->parent->left)
                {
                    Node* uncle = inserted->parent->parent->right;
                    // 叔红
                    if (uncle != null && uncle->color == Color.Red)
                    {
                        inserted->parent->color = Color.Black;
                        uncle->color = Color.Black;
                        inserted->parent->parent->color = Color.Red;
                        inserted = inserted->parent->parent;
                    }
                    // 叔黑
                    else
                    {
                        // 当前结点是父结点的右子
                        if (inserted == inserted->parent->right)
                        {
                            inserted = inserted->parent;
                            LeftRotate(inserted);
                        }
                        // 当前结点是父结点的左子
                        else
                        {
                            inserted->parent->color = Color.Black;
                            inserted->parent->parent->color = Color.Red;
                            RightRotate(inserted->parent->parent);
                        }
                    }
                }
                // 父结点是祖父结点的右子
                else
                {
                    Node* uncle = inserted->parent->parent->left;
                    // 叔红
                    if (uncle != null && uncle->color == Color.Red)
                    {
                        inserted->parent->color = Color.Black;
                        uncle->color = Color.Black;
                        inserted->parent->parent->color = Color.Red;
                        inserted = inserted->parent->parent;
                    }
                    // 叔黑
                    else
                    {
                        // 当前结点是父结点的左子
                        if (inserted == inserted->parent->left)
                        {
                            inserted = inserted->parent;
                            RightRotate(inserted);
                        }
                        // 当前结点是父结点的右子
                        else
                        {
                            inserted->parent->color = Color.Black;
                            inserted->parent->parent->color = Color.Red;
                            LeftRotate(inserted->parent->parent);
                        }
                    }
                }
            }
            // 根为黑
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
                // node是黑色无子节点且有兄弟，此时假装node == null
                if (node == node->parent->right)
                // 兄在左
                {
                    Node* bro = node->parent->left;
                    if (bro->color == Color.Black)
                    {
                        // 兄黑
                        bool broLBlack = bro->left == null || (bro->left->color == Color.Black);
                        bool broRBlack = bro->right == null || (bro->right->color == Color.Black);
                        // 兄子全黑
                        if (broLBlack && broRBlack)
                        {
                            // 父黑
                            if (node->parent->color == Color.Black)
                            {
                                bro->color = Color.Red;
                                node = node->parent;
                            }
                            // 父红
                            else
                            {
                                bro->color = Color.Red;
                                node->parent->color = Color.Black;
                                break;
                            }
                        }
                        // 兄子不全黑
                        else
                        {
                            // 兄左子黑
                            if (broLBlack)
                            {
                                bro->color = Color.Red;
                                bro->right->color = Color.Black;
                                LeftRotate(bro);
                            }
                            // 兄左子红
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
                    // 兄红，则兄一定有子节点
                    else
                    {
                        RightRotate(node->parent);
                        node->parent->color = Color.Red;
                        bro->color = Color.Black;
                    }
                }
                // 兄在右
                else
                {
                    Node* bro = node->parent->right;
                    // 兄黑
                    if (bro->color == Color.Black)
                    {
                        bool broLBlack = bro->left == null || (bro->left->color == Color.Black);
                        bool broRBlack = bro->right == null || (bro->right->color == Color.Black);
                        // 兄子全黑
                        if (broLBlack && broRBlack)
                        {
                            // 父黑
                            if (node->parent->color == Color.Black)
                            {
                                bro->color = Color.Red;
                                node = node->parent;
                            }
                            // 父红
                            else
                            {
                                bro->color = Color.Red;
                                node->parent->color = Color.Black;
                                break;
                            }
                        }
                        // 兄子不全黑
                        else
                        {
                            // 兄右子黑
                            if (broRBlack)
                            {
                                bro->color = Color.Red;
                                bro->left->color = Color.Black;
                                RightRotate(bro);
                            }
                            // 兄右子红
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
                    // 兄红，则兄一定有子节点
                    else
                    {
                        LeftRotate(node->parent);
                        node->parent->color = Color.Red;
                        bro->color = Color.Black;
                    }
                }
            }
            // 根节点必须是黑
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
        /// 请求地址
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
        /// 释放地址
        /// </summary>
        /// <param name="node"></param>
        private void Free(Node* node)
        {
            // 解除父连接
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
            // 是最后一个element
            if (index == m_Head->data.Count - 1)
            {
                // 直接移除
                m_Head->data.RemoveLast();
            }
            // 不是最后一个element
            else
            {
                // 将最后一个element移动到node
                m_Head->data.SwapLastAndRemove(index);
                // 下标设置为当前node下标
                node->index = index;
                // 由于最后的element地址变为node，需要重新连接父子结点
                // 是根节点
                if (node->parent == null)
                {
                    m_Head->root = node;
                }
                // 是父节点的左子节点
                else if (node->key.CompareTo(node->parent->key) < 0)
                {
                    node->parent->left = node;
                }
                // 是父节点的右子节点
                else
                {
                    node->parent->right = node;
                }
                // 重连接子节点
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