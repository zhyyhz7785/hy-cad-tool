using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;

namespace HyCADTool.Features.Tables.ViewModels
{
    public sealed class TableMatrixCellVm : INotifyPropertyChanged
    {
        private readonly Action<CellAddr, string> _commit;
        private string _text;

        public TableMatrixCellVm(TableMatrixCellItem item, Action<CellAddr, string> commit)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _commit = commit;
            Address = item.Address;
            IsHidden = item.IsHidden;
            IsEditable = item.IsEditable;
            _text = item.Text ?? string.Empty;
        }

        public CellAddr Address { get; }

        public bool IsHidden { get; }

        public bool IsEditable { get; }

        public bool IsReadOnly => !IsEditable;

        public string Text
        {
            get => _text;
            set
            {
                if (!IsEditable || _text == value)
                    return;

                _text = value ?? string.Empty;
                OnPropertyChanged();
                _commit?.Invoke(Address, _text);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
