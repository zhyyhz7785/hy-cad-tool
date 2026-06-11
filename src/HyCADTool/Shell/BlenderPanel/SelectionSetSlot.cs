using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>
    /// 过滤面板选择集槽位（会话级内存，固定 1~5）。
    /// </summary>
    public sealed class SelectionSetSlot : INotifyPropertyChanged
    {
        private ObjectId[] _ids = Array.Empty<ObjectId>();

        public SelectionSetSlot(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public ObjectId[] Ids
        {
            get => _ids;
            private set
            {
                _ids = value ?? Array.Empty<ObjectId>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(Label));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public int Count => _ids.Length;

        public bool IsEmpty => Count == 0;

        /// <summary>空槽显示序号；已存显示「1·23」。</summary>
        public string Label => IsEmpty ? Index.ToString() : $"{Index}·{Count}";

        /// <summary>运算框下拉显示：「1 (23)」或「1 (空)」。</summary>
        public string DisplayName => IsEmpty ? $"{Index} (空)" : $"{Index} ({Count})";

        public void SetIds(ObjectId[] ids)
        {
            Ids = ids ?? Array.Empty<ObjectId>();
        }

        public void Clear()
        {
            Ids = Array.Empty<ObjectId>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
