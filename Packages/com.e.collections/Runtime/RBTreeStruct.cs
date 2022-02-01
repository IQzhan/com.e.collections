using System;
using System.Diagnostics;

namespace E.Collections.Unsafe
{
    internal enum RBTColor
    {
        Red = 0,
        Black = 1
    }

    internal unsafe struct RBTree<Key>
        where Key : unmanaged, IComparable<Key>
    {
        public RBTNode<Key>* root;
    }

    internal unsafe struct RBTNode<Key>
        where Key : unmanaged, IComparable<Key>
    {
        internal RBTNode<Key>* parent;
        internal RBTNode<Key>* left;
        internal RBTNode<Key>* right;
        //[31]   color
        //[30]   isDirty
        //[29~0] index
        internal uint m_Marks;
        internal Key m_Key;
        public int Index { get => (int)(m_Marks & 0x3FFFFFFF); set => m_Marks = m_Marks & 0xC0000000 | (uint)value; }
        public RBTColor Color { get => (RBTColor)(m_Marks >> 31); set => m_Marks = m_Marks & 0x7FFFFFFF | ((uint)value << 31); }
        public bool IsDirty { get => (m_Marks & 0x40000000) == 0x40000000; set => m_Marks = m_Marks & 0xBFFFFFFF | ((uint)(value ? 0x40000000 : 0)); }
    }

    internal unsafe struct RBTreeStruct<Key, Functions>
        where Key : unmanaged, IComparable<Key>
        where Functions : unmanaged, RBTreeStruct<Key, Functions>.IFunctions
    {
        public interface IFunctions
        {
            public RBTree<Key>* GetTree(Key key);
        }

        public readonly Functions functions;

        public readonly UnsafeChunkedList data;

        public RBTreeStruct(Functions functions, UnsafeChunkedList data)
        {
            this.functions = functions;
            this.data = data;
        }

        public bool TryGetNode(RBTree<Key>* tree, Key key, out RBTNode<Key>* node)
        {
            node = tree->root;
            while (node != null)
            {
                int compare = key.CompareTo(node->m_Key);
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

        public RBTNode<Key>* Set(RBTree<Key>* tree, Key key)
        {
            RBTNode<Key>* node, parent;
            node = tree->root;
            parent = node;
            while (node != null)
            {
                parent = node;
                int compare = key.CompareTo(node->m_Key);
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
                    return node;
                }
            }
            //初始化红色节点
            node = Malloc();
            node->m_Key = key;
            node->parent = parent;
            //与父连接
            if (tree->root == null)
            {
                tree->root = node;
            }
            else if (key.CompareTo(parent->m_Key) < 0)
            {
                parent->left = node;
            }
            else
            {
                parent->right = node;
            }
            //将普通二叉树修正为红黑树
            AddFixup(tree, node);
            return node;
        }

        public bool Remove(RBTree<Key>* tree, Key key)
        {
            if (tree->root == null)
            {
                return false;
            }
            RBTNode<Key>* toDelete = tree->root;
            while (toDelete != null)
            {
                int compare = key.CompareTo(toDelete->m_Key);
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
            RBTNode<Key>* forReplace = null;
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

        private void AddFixup(RBTree<Key>* tree, RBTNode<Key>* inserted)
        {
            if (inserted == tree->root)
            {
                inserted->Color = RBTColor.Black;
                return;
            }
            // 父为红色时才需要调整
            while (inserted->parent != null && inserted->parent->Color == RBTColor.Red)
            {
                // 父结点是祖父结点的左子
                if (inserted->parent == inserted->parent->parent->left)
                {
                    RBTNode<Key>* uncle = inserted->parent->parent->right;
                    // 叔红
                    if (uncle != null && uncle->Color == RBTColor.Red)
                    {
                        inserted->parent->Color = RBTColor.Black;
                        uncle->Color = RBTColor.Black;
                        inserted->parent->parent->Color = RBTColor.Red;
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
                            inserted->parent->Color = RBTColor.Black;
                            inserted->parent->parent->Color = RBTColor.Red;
                            RightRotate(inserted->parent->parent);
                        }
                    }
                }
                // 父结点是祖父结点的右子
                else
                {
                    RBTNode<Key>* uncle = inserted->parent->parent->left;
                    // 叔红
                    if (uncle != null && uncle->Color == RBTColor.Red)
                    {
                        inserted->parent->Color = RBTColor.Black;
                        uncle->Color = RBTColor.Black;
                        inserted->parent->parent->Color = RBTColor.Red;
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
                            inserted->parent->Color = RBTColor.Black;
                            inserted->parent->parent->Color = RBTColor.Red;
                            LeftRotate(inserted->parent->parent);
                        }
                    }
                }
            }
            // 根为黑
            tree->root->Color = RBTColor.Black;
        }

        private void SwapUpDown(RBTree<Key>* tree, RBTNode<Key>* up, RBTNode<Key>* down)
        {
            RBTColor c = up->Color;
            up->Color = down->Color;
            down->Color = c;

            //swap parent
            RBTNode<Key>* swap = down->parent;
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

        private void RemoveFixup(RBTree<Key>* tree, RBTNode<Key>* node)
        {
            if (node->Color == RBTColor.Red || node == tree->root)
            {
                return;
            }
            while (node != tree->root)
            {
                // node是黑色无子节点且有兄弟，此时假装node == null
                if (node == node->parent->right)
                // 兄在左
                {
                    RBTNode<Key>* bro = node->parent->left;
                    if (bro->Color == RBTColor.Black)
                    {
                        // 兄黑
                        bool broLBlack = bro->left == null || (bro->left->Color == RBTColor.Black);
                        bool broRBlack = bro->right == null || (bro->right->Color == RBTColor.Black);
                        // 兄子全黑
                        if (broLBlack && broRBlack)
                        {
                            // 父黑
                            if (node->parent->Color == RBTColor.Black)
                            {
                                bro->Color = RBTColor.Red;
                                node = node->parent;
                            }
                            // 父红
                            else
                            {
                                bro->Color = RBTColor.Red;
                                node->parent->Color = RBTColor.Black;
                                break;
                            }
                        }
                        // 兄子不全黑
                        else
                        {
                            // 兄左子黑
                            if (broLBlack)
                            {
                                bro->Color = RBTColor.Red;
                                bro->right->Color = RBTColor.Black;
                                LeftRotate(bro);
                            }
                            // 兄左子红
                            else
                            {
                                RBTColor c = node->parent->Color;
                                node->parent->Color = bro->Color;
                                bro->Color = c;
                                bro->left->Color = RBTColor.Black;
                                RightRotate(node->parent);
                                break;
                            }
                        }
                    }
                    // 兄红，则兄一定有子节点
                    else
                    {
                        RightRotate(node->parent);
                        node->parent->Color = RBTColor.Red;
                        bro->Color = RBTColor.Black;
                    }
                }
                // 兄在右
                else
                {
                    RBTNode<Key>* bro = node->parent->right;
                    // 兄黑
                    if (bro->Color == RBTColor.Black)
                    {
                        bool broLBlack = bro->left == null || (bro->left->Color == RBTColor.Black);
                        bool broRBlack = bro->right == null || (bro->right->Color == RBTColor.Black);
                        // 兄子全黑
                        if (broLBlack && broRBlack)
                        {
                            // 父黑
                            if (node->parent->Color == RBTColor.Black)
                            {
                                bro->Color = RBTColor.Red;
                                node = node->parent;
                            }
                            // 父红
                            else
                            {
                                bro->Color = RBTColor.Red;
                                node->parent->Color = RBTColor.Black;
                                break;
                            }
                        }
                        // 兄子不全黑
                        else
                        {
                            // 兄右子黑
                            if (broRBlack)
                            {
                                bro->Color = RBTColor.Red;
                                bro->left->Color = RBTColor.Black;
                                RightRotate(bro);
                            }
                            // 兄右子红
                            else
                            {
                                RBTColor c = node->parent->Color;
                                node->parent->Color = bro->Color;
                                bro->Color = c;
                                bro->right->Color = RBTColor.Black;
                                LeftRotate(node->parent);
                                break;
                            }
                        }
                    }
                    // 兄红，则兄一定有子节点
                    else
                    {
                        LeftRotate(node->parent);
                        node->parent->Color = RBTColor.Red;
                        bro->Color = RBTColor.Black;
                    }
                }
            }
            // 根节点必须是黑
            tree->root->Color = RBTColor.Black;
        }

        private void LeftRotate(RBTNode<Key>* node)
        {
            if (node == null)
            {
                return;
            }
            RBTNode<Key>* nodeR = node->right;
            if (nodeR == null)
            {
                return;
            }
            node->right = nodeR->left;
            if (node->right != null)
            {
                node->right->parent = node;
            }
            RBTNode<Key>* nodeP = node->parent;
            nodeR->parent = nodeP;
            if (nodeP == null)
            {
                functions.GetTree(node->m_Key)->root = nodeR;
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

        private void RightRotate(RBTNode<Key>* node)
        {
            if (node == null)
            {
                return;
            }
            RBTNode<Key>* nodeL = node->left;
            if (nodeL == null)
            {
                return;
            }
            node->left = nodeL->right;
            if (null != node->left)
            {
                node->left->parent = node;
            }
            RBTNode<Key>* nodeP = node->parent;
            nodeL->parent = nodeP;
            if (nodeP == null)
            {
                functions.GetTree(node->m_Key)->root = nodeL;
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

        private RBTNode<Key>* FindMin(RBTNode<Key>* t)
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

        private RBTNode<Key>* Malloc()
        {
            int index = data.Count;
            RBTNode<Key>* ptr = (RBTNode<Key>*)data.Add().Value;
            *ptr = default;
            //[31]   color = Color.Red
            //[30]   isDirty == false
            //[29~0] index
            ptr->m_Marks = (uint)index;
            return ptr;
        }

        private void Free(RBTNode<Key>* node)
        {
            // 解除父连接
            ref var root = ref functions.GetTree(node->m_Key)->root;
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
            int index = node->Index;
            UnsafeChunkedList data = this.data;
            if (index == data.Count - 1)
            {
                data.RemoveLast();
            }
            else
            {
                data.SwapLastAndRemove(index);
                node->Index = index;
                // 由于最后的element地址变为node，需要重新连接父子结点
                // 是根节点
                if (node->parent == null)
                {
                    functions.GetTree(node->m_Key)->root = node;
                }
                // 是父节点的左子节点
                else if (node->m_Key.CompareTo(node->parent->m_Key) < 0)
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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal void Check(RBTree<Key>* tree)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int pathBlackCount = GetPathBlackCount(tree->root);
            if (pathBlackCount == 0)
            {
                throw new Exception("This is not a red-black-tree.");
            }
#endif
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private int GetPathBlackCount(RBTNode<Key>* node)
        {
            int leftCount = 0, rightCount = 0;
            if (node != null)
            {
                leftCount = GetPathBlackCount(node->left);
                rightCount = GetPathBlackCount(node->right);
            }
            if (node != null && node->Color == RBTColor.Red)
            {
                bool leftBlack = node->left == null || node->left->Color == RBTColor.Black;
                bool rightBlack = node->right == null || node->right->Color == RBTColor.Black;
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