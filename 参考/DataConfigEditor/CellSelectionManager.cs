#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// 单元格多选管理器
    /// 支持 Ctrl+点击切换、Shift+点击范围选、批量编辑
    /// </summary>
    public class CellSelectionManager
    {
        private readonly HashSet<CellCoord> _selectedCells = new();
        private CellCoord? _lastClickedCell;
        private CellCoord? _anchorCell; // Shift 范围选的锚点

        public struct CellCoord : IEquatable<CellCoord>
        {
            public int Col; // 属性列索引（0=属性名列）
            public int Row; // 实例行索引

            public bool Equals(CellCoord other) => Col == other.Col && Row == other.Row;
            public override bool Equals(object? obj) => obj is CellCoord other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Col, Row);
            public override string ToString() => $"({Col}, {Row})";
        }

        /// <summary>当前所有选中的单元格</summary>
        public IReadOnlySet<CellCoord> SelectedCells => _selectedCells;

        /// <summary>是否有选中的单元格</summary>
        public bool HasSelection => _selectedCells.Count > 0;

        /// <summary>选中数量</summary>
        public int SelectionCount => _selectedCells.Count;

        /// <summary>
        /// 处理单元格点击事件
        /// </summary>
        /// <param name="col">列索引</param>
        /// <param name="row">行索引</param>
        /// <param name="ctrlPressed">Ctrl 键是否按下</param>
        /// <param name="shiftPressed">Shift 键是否按下</param>
        /// <returns>返回需要刷新的单元格坐标列表</returns>
        public List<CellCoord> HandleClick(int col, int row, bool ctrlPressed, bool shiftPressed)
        {
            var changed = new List<CellCoord>();
            var current = new CellCoord { Col = col, Row = row };

            if (shiftPressed && _anchorCell != null)
            {
                // Shift 范围选：从锚点到当前格的矩形
                var oldSelected = new HashSet<CellCoord>(_selectedCells);
                _selectedCells.Clear();

                int minCol = Math.Min(_anchorCell.Value.Col, col);
                int maxCol = Math.Max(_anchorCell.Value.Col, col);
                int minRow = Math.Min(_anchorCell.Value.Row, row);
                int maxRow = Math.Max(_anchorCell.Value.Row, row);

                for (int c = minCol; c <= maxCol; c++)
                {
                    for (int r = minRow; r <= maxRow; r++)
                    {
                        _selectedCells.Add(new CellCoord { Col = c, Row = r });
                    }
                }

                // 计算变化
                foreach (var cell in oldSelected)
                    if (!_selectedCells.Contains(cell)) changed.Add(cell);
                foreach (var cell in _selectedCells)
                    if (!oldSelected.Contains(cell)) changed.Add(cell);
            }
            else if (ctrlPressed)
            {
                // Ctrl 切换当前格
                if (_selectedCells.Contains(current))
                {
                    _selectedCells.Remove(current);
                    changed.Add(current);
                }
                else
                {
                    _selectedCells.Add(current);
                    changed.Add(current);
                }

                _anchorCell = current;
            }
            else
            {
                // 普通点击：清空选中，选中当前格
                foreach (var cell in _selectedCells)
                    changed.Add(cell);

                _selectedCells.Clear();
                _selectedCells.Add(current);
                _anchorCell = current;
                changed.Add(current);
            }

            _lastClickedCell = current;
            return changed;
        }

        /// <summary>
        /// 清空所有选中
        /// </summary>
        public List<CellCoord> ClearSelection()
        {
            var changed = new List<CellCoord>(_selectedCells);
            _selectedCells.Clear();
            _lastClickedCell = null;
            _anchorCell = null;
            return changed;
        }

        /// <summary>
        /// 判断单元格是否被选中
        /// </summary>
        public bool IsSelected(int col, int row)
        {
            return _selectedCells.Contains(new CellCoord { Col = col, Row = row });
        }

        /// <summary>
        /// 获取所有选中的列索引（用于批量编辑判断）
        /// </summary>
        public HashSet<int> GetSelectedColumns()
        {
            var cols = new HashSet<int>();
            foreach (var cell in _selectedCells)
                cols.Add(cell.Col);
            return cols;
        }

        /// <summary>
        /// 获取所有选中的行索引
        /// </summary>
        public HashSet<int> GetSelectedRows()
        {
            var rows = new HashSet<int>();
            foreach (var cell in _selectedCells)
                rows.Add(cell.Row);
            return rows;
        }

        /// <summary>
        /// 判断选中的是否是同一列（可以批量修改）
        /// </summary>
        public bool IsSingleColumnSelection()
        {
            if (_selectedCells.Count <= 1) return false;
            int firstCol = -1;
            foreach (var cell in _selectedCells)
            {
                if (firstCol < 0) firstCol = cell.Col;
                else if (cell.Col != firstCol) return false;
            }
            return true;
        }
    }
}
#endif
