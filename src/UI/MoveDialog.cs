using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MultiVirtDesk.Storage;
using MultiVirtDesk.VirtualDesktop;

namespace MultiVirtDesk.UI
{
    public class MoveDialog : Form
    {
        private string _targetItemPath;
        private DesktopStorageManager _storage;
        private VirtualDesktopService _vdService;
        private ListBox _listBox;
        private Button _btnMove;
        private Button _btnCancel;
        private List<DesktopInfo> _desktops;

        public MoveDialog(string itemPath, DesktopStorageManager storage, VirtualDesktopService vdService)
        {
            _targetItemPath = itemPath;
            _storage = storage;
            _vdService = vdService;

            InitializeComponent();
            LoadDesktops();
        }

        private void InitializeComponent()
        {
            string filename = Path.GetFileName(_targetItemPath);

            this.Text = "Move to Virtual Desktop";
            this.Size = new Size(360, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.TopMost = true;

            Label lbl = new Label
            {
                Text = string.Format("Move \"{0}\" to:", filename),
                Location = new Point(16, 16),
                Size = new Size(310, 24),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };
            this.Controls.Add(lbl);

            _listBox = new ListBox
            {
                Location = new Point(16, 44),
                Size = new Size(310, 180),
                Font = new Font("Segoe UI", 9.5f)
            };
            _listBox.DoubleClick += (s, e) => ExecuteMove();
            this.Controls.Add(_listBox);

            _btnMove = new Button
            {
                Text = "Move",
                Location = new Point(150, 236),
                Size = new Size(84, 30),
                DialogResult = DialogResult.OK
            };
            _btnMove.Click += (s, e) => ExecuteMove();
            this.Controls.Add(_btnMove);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(242, 236),
                Size = new Size(84, 30),
                DialogResult = DialogResult.Cancel
            };
            _btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnMove;
            this.CancelButton = _btnCancel;
        }

        private void LoadDesktops()
        {
            _desktops = _vdService.GetDesktops();
            DesktopInfo current = _vdService.GetCurrentDesktop();
            Guid curId = current != null ? current.Id : Guid.Empty;

            foreach (DesktopInfo desk in _desktops)
            {
                string label = string.Format("{0}. {1}{2}", desk.Index + 1, desk.Name, desk.Id == curId ? " (Current)" : "");
                _listBox.Items.Add(label);
            }

            if (_listBox.Items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }
        }

        private void ExecuteMove()
        {
            int idx = _listBox.SelectedIndex;
            if (idx >= 0 && idx < _desktops.Count)
            {
                DesktopInfo target = _desktops[idx];
                _storage.MoveItemToDesktop(_targetItemPath, target.Id);
                this.Close();
            }
        }
    }
}
