using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace E.Collections.Unsafe
{
    /// <summary>
    /// Hash + Red-Black-Tree, faster then UnsafeChunkedSet
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    public unsafe struct UnsafeChunkedHashSet<Key> : ICollection, IPtrIndexable, IChunked, IResizeable, ILockable, IDisposable, IEquatable<UnsafeChunkedHashSet<Key>>
        where Key : unmanaged, IComparable<Key>
    {
        #region Main

        private struct Head
        {
            public int existsMark;
            public Allocator allocator;
            public int preSize;
            public int mapLength;
            public int lockedMark;
            public Tree* map;
            public UnsafeChunkedList data;
        }

        private struct Tree
        {
            public Node* root;
        }

        private enum Color
        {
            Black = 0,
            Red = 1
        }

        private struct Node
        {
            public Node* parent;
            public Node* left;
            public Node* right;
            private uint m_marks;
            public int index { get => (int)(m_marks & 0x7FFFFFFF); set => m_marks = m_marks & 0x80000000 | (uint)value; }
            public Color color { get => (Color)(m_marks >> 31); set => m_marks = m_marks & 0x7FFFFFFF | (uint)value << 31; }
            public int belongs;
            public Key key;
        }

        [NativeDisableUnsafePtrRestriction]
        private Head* m_Head;

        private const int ExistsMark = 1000004;

        public bool IsCreated => m_Head != null && m_Head->existsMark == ExistsMark;

        public int Count => IsCreated ? m_Head->data.Count : 0;

        public long ChunkSize => IsCreated ? m_Head->data.ChunkSize : 0;

        public int ChunkCount => IsCreated ? m_Head->data.ChunkCount : 0;

        public int ElementSize => IsCreated ? m_Head->data.ElementSize : 0;

        public UnsafeChunkedHashSet(int hashMapLength, int valueSize, long chunkSize, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (hashMapLength <= 0)
            {
                throw new ArgumentException("hashMapLength must bigger then 0.");
            }
            if (valueSize <= 0)
            {
                throw new ArgumentException("elementSize must bigger then 0.");
            }
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must bigger then 0.");
            }
            if (valueSize > chunkSize)
            {
                throw new ArgumentException("chunkSize must bigger then elementSize.");
            }
#endif
            int preSize = Memory.SizeOf<Node>();
            int nodeSize = preSize + valueSize;
            m_Head = (Head*)Memory.Malloc<Head>(1, allocator);
            *m_Head = new Head()
            {
                existsMark = ExistsMark,
                allocator = allocator,
                preSize = preSize,
                mapLength = hashMapLength,
                map = (Tree*)Memory.Malloc<Tree>(hashMapLength, allocator),
                data = new UnsafeChunkedList(nodeSize, chunkSize, allocator)
            };
            Memory.Clear<Tree>(m_Head->map, hashMapLength);
        }

        public byte* this[int index] => GetValueByIndex(index);

        public bool Contains(Key key)
        {
            CheckExists();
            Tree* tree = GetTree(key);
            return TryGetNode(tree, key, out var _);
        }

        public int IndexOf(Key key)
        {
            CheckExists();
            Tree* tree = GetTree(key);
            if (TryGetNode(tree, key, out Node* node))
            {
                return node->index;
            }
            return -1;
        }

        public int IndexOf(Key key, out byte* value)
        {
            CheckExists();
            Tree* tree = GetTree(key);
            if (TryGetNode(tree, key, out Node* node))
            {
                value = (byte*)node + m_Head->preSize;
                return node->index;
            }
            value = null;
            return -1;
        }

        public Key GetKeyByIndex(int index)
        {
            CheckExists();
            CheckIndex(index);
            Key key = ((Node*)m_Head->data[index])->key;
            return key;
        }

        public byte* GetValueByIndex(int index)
        {
            CheckExists();
            CheckIndex(index);
            byte* value = m_Head->data[index] + m_Head->preSize;
            return value;
        }

        public void GetKeyValueByIndex(int index, out Key key, out byte* value)
        {
            CheckExists();
            CheckIndex(index);
            byte* node = m_Head->data[index];
            key = ((Node*)node)->key;
            value = node + m_Head->preSize;
        }

        public Key GetKeyByValue(byte* value)
        {
            CheckExists();
            Key key = ((Node*)(value - m_Head->preSize))->key;
            return key;
        }

        public byte* Set(Key key)
        {
            CheckExists();
            Tree* tree = GetTree(key);
            return InternalSet(tree, key);
        }

        public bool Remove(Key key)
        {
            CheckExists();
            Tree* tree = GetTree(key);
            return InternalRemove(tree, key);
        }

        public void Extend(int count)
        {
            CheckExists();
            m_Head->data.Extend(count);
        }

        public void Clear()
        {
            CheckExists();
            Memory.Clear<Tree>(m_Head->map, m_Head->mapLength);
            m_Head->data.Clear();
        }

        public void Dispose()
        {
            CheckExists();
            m_Head->existsMark = 0;
            Memory.Free(m_Head->map, m_Head->allocator);
            m_Head->data.Dispose();
            Memory.Free(m_Head, m_Head->allocator);
            m_Head = null;
        }

        public Lock GetLock()
        {
            CheckExists();
            return new Lock(&m_Head->lockedMark);
        }

        public override bool Equals(object obj) => obj is UnsafeChunkedHashSet<Key> set && m_Head == set.m_Head;

        public bool Equals(UnsafeChunkedHashSet<Key> other) => m_Head == other.m_Head;

        public override int GetHashCode() => m_Head != null ? (int)m_Head : 0;

        public static bool operator ==(UnsafeChunkedHashSet<Key> left, UnsafeChunkedHashSet<Key> right) => left.m_Head == right.m_Head;

        public static bool operator !=(UnsafeChunkedHashSet<Key> left, UnsafeChunkedHashSet<Key> right) => left.m_Head != right.m_Head;

        #endregion

        #region Internal

        private Tree* GetTree(Key key)
        {
            return m_Head->map + ((key.GetHashCode() % m_Head->mapLength) & 0x7FFFFFFF);
        }

        private bool TryGetNode(Tree* tree, Key key, out Node* node)
        {
            node = tree->root;
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
                    return true;
                }
            }
            return false;
        }

        private byte* InternalSet(Tree* tree, Key key)
        {
            Node* node, parent;
            node = tree->root;
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
                    return res;
                }
            }
            //初始化红色节点
            node = Malloc(tree);
            node->key = key;
            node->color = Color.Red;
            node->left = node->right = null;
            node->parent = parent;
            res = (byte*)node + m_Head->preSize;
            //与父连接
            if (tree->root == null)
            {
                tree->root = node;
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
            AddFixup(tree, node);
            return res;
        }

        private bool InternalRemove(Tree* tree, Key key)
        {
            if (tree->root == null)
            {
                return false;
            }
            Node* toDelete = tree->root;
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
                return false;
            }
            Node* forReplace = null;
            bool hasLeft, hasRight;
            while ((hasLeft = toDelete->left != null) | (hasRight = toDelete->right != null))
            {
                // 如果两个子节点
                if (hasLeft && hasRight)
                {
                    //找到右子树中最小的结点
                    forReplace = FindMin(toDelete->right);
                }
                // 节点为黑，且只有红右子节点
                else if (!hasLeft && hasRight)
                {
                    forReplace = toDelete->right;
                }
                // 节点为黑，且只有红左子节点
                else if (hasLeft && !hasRight)
                {
                    forReplace = toDelete->left;
                }
                // 交换节点位置，并保证颜色在位置上不变
                SwapUpDown(tree, toDelete, forReplace);
            }
            // 修正红黑树，红色跳过
            RemoveFixup(tree, toDelete);
            // 释放
            Free(toDelete);
            return true;
        }

        private void AddFixup(Tree* tree, Node* inserted)
        {
            if (inserted == tree->root)
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
            tree->root->color = Color.Black;
        }

        private void SwapUpDown(Tree* tree, Node* up, Node* down)
        {
            Color c = up->color;
            up->color = down->color;
            down->color = c;

            //swap parent
            Node* swap = down->parent;
            down->parent = up->parent;
            up->parent = swap;
            if (up->parent != null)
            {
                if (up->parent == up)
                {
                    up->parent = down;
                }
                else if (up->parent->left == down)
                {
                    up->parent->left = up;
                }
                else
                {
                    up->parent->right = up;
                }
            }
            if (down->parent != null)
            {
                if (down->parent->left == up)
                {
                    down->parent->left = down;
                }
                else
                {
                    down->parent->right = down;
                }
            }
            else
            {
                tree->root = down;
            }

            //swap left
            swap = down->left;
            down->left = up->left;
            up->left = swap;
            if (up->left != null)
            {
                up->left->parent = up;
            }
            if (down->left != null)
            {
                if (down->left == down)
                {
                    down->left = up;
                }
                else
                {
                    down->left->parent = down;
                }
            }

            //swap right
            swap = down->right;
            down->right = up->right;
            up->right = swap;
            if (up->right != null)
            {
                up->right->parent = up;
            }
            if (down->right != null)
            {
                if (down->right == down)
                {
                    down->right = up;
                }
                else
                {
                    down->right->parent = down;
                }
            }
        }

        private void RemoveFixup(Tree* tree, Node* node)
        {
            if (node->color == Color.Red || node == tree->root)
            {
                return;
            }
            while (node != tree->root)
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
            tree->root->color = Color.Black;
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
                (m_Head->map + node->belongs)->root = nodeR;
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
                (m_Head->map + node->belongs)->root = nodeL;
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

        private Node* Malloc(Tree* tree)
        {
            int index = m_Head->data.Count;
            Node* ptr = (Node*)m_Head->data.Add();
            ptr->index = index;
            ptr->belongs = (int)(tree - m_Head->map);
            return ptr;
        }

        private void Free(Node* node)
        {
            // 解除父连接
            ref var root = ref (m_Head->map + node->belongs)->root;
            if (node == root)
            {
                root = null;
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
            UnsafeChunkedList data = m_Head->data;
            if (index == data.Count - 1)
            {
                data.RemoveLast();
            }
            else
            {
                data.SwapLastAndRemove(index);
                node->index = index;
                // 由于最后的element地址变为node，需要重新连接父子结点
                // 是根节点
                if (node->parent == null)
                {
                    (m_Head->map + node->belongs)->root = node;
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

        #endregion

        #region Check

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckExists()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Head == null || m_Head->existsMark != ExistsMark)
            {
                throw new NullReferenceException($"{nameof(UnsafeChunkedSet<Key>)} is yet created or already disposed.");
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= m_Head->data.Count)
            {
                throw new IndexOutOfRangeException($"{nameof(UnsafeChunkedSet<Key>)} index must >= 0 && < Count.");
            }
#endif
        }

        #endregion
    }
}