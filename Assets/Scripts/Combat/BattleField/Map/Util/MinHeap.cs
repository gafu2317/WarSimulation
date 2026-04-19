using System.Collections.Generic;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// float 優先度付きの最小ヒープ。Priority-Flood 等、ダイクストラ系の処理用。
    /// .NET 6 の <c>System.Collections.Generic.PriorityQueue</c> を使わないのは、
    /// Unity のバージョンや Scripting Runtime によって利用可否が揺れるため。
    /// </summary>
    internal sealed class MinHeap<T>
    {
        private struct Node
        {
            public T Item;
            public float Priority;
        }

        private readonly List<Node> _nodes;

        public MinHeap(int capacity = 16) => _nodes = new List<Node>(capacity);

        public int Count => _nodes.Count;

        public void Push(T item, float priority)
        {
            _nodes.Add(new Node { Item = item, Priority = priority });
            SiftUp(_nodes.Count - 1);
        }

        public T Pop(out float priority)
        {
            Node top = _nodes[0];
            int last = _nodes.Count - 1;
            _nodes[0] = _nodes[last];
            _nodes.RemoveAt(last);
            if (_nodes.Count > 0) SiftDown(0);
            priority = top.Priority;
            return top.Item;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_nodes[parent].Priority <= _nodes[i].Priority) break;
                (_nodes[parent], _nodes[i]) = (_nodes[i], _nodes[parent]);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = _nodes.Count;
            while (true)
            {
                int l = 2 * i + 1;
                int r = 2 * i + 2;
                int smallest = i;
                if (l < n && _nodes[l].Priority < _nodes[smallest].Priority) smallest = l;
                if (r < n && _nodes[r].Priority < _nodes[smallest].Priority) smallest = r;
                if (smallest == i) break;
                (_nodes[smallest], _nodes[i]) = (_nodes[i], _nodes[smallest]);
                i = smallest;
            }
        }
    }
}
