using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	public partial class FormCamGeneralConfig : Form
	{

        private readonly AppConfig _config;
        private readonly string _configPath;

        private readonly BindingList<PosGeneralRow> _rows = new BindingList<PosGeneralRow>();
        private bool _gridBuilt;

        public event EventHandler ConfigSaved;

        public FormCamGeneralConfig(AppConfig config, string configPath)
        {
            _config = config;
            _configPath = configPath;

            InitializeComponent();

            // 事件（只绑一次，避免重复弹“保存成功”）
            this.Load += Form_Load;
            CboJob.SelectedIndexChanged += CboJob_SelectedIndexChanged;
            CboCam.SelectedIndexChanged += CboCam_SelectedIndexChanged;

            btnAddRow.Click += BtnAddRow_Click;
            btnDeleteRow.Click += BtnDeleteRow_Click;
            btnSave.Click += BtnSave_Click;
            btnAddCam.Click += BtnAddCam_Click;
            btnDeleteCam.Click += BtnDeleteCam_Click;

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.MultiSelect = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.DataSource = _rows;
        }

        #region 加载/联动

        private void Form_Load(object sender, EventArgs e)
        {
            BuildGridColumns();   // 只建一次列
            FillJobs();
            if (CboJob.Items.Count > 0) CboJob.SelectedIndex = 0;
        }

        private void CboJob_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillCams();
            RefreshGrid();
            UpdateCamCountLabel();
        }

        private void CboCam_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshGrid();
        }

        private void FillJobs()
        {
            CboJob.Items.Clear();
            if (_config?.Models != null)
            {
                foreach (var m in _config.Models)
                    CboJob.Items.Add(m.Name ?? "");
            }
        }

        private void FillCams()
        {
            var job = GetCurrentJob();
            string old = CboCam.SelectedItem as string;

            CboCam.Items.Clear();
            if (job?.Cameras == null) return;

            foreach (var cam in job.Cameras)
                CboCam.Items.Add(cam.Name ?? "");

            // 尽量还原旧选择
            if (!string.IsNullOrEmpty(old) && CboCam.Items.Contains(old))
                CboCam.SelectedItem = old;
            else if (CboCam.Items.Count > 0)
                CboCam.SelectedIndex = 0;
        }

        private void UpdateCamCountLabel()
        {
            var job = GetCurrentJob();
            int cnt = job?.Cameras?.Count ?? 0;
            lblCamCount.Text = cnt.ToString();            // CamN 自动显示
            if (job?.General == null) job.General = new GeneralConfig();
            job.General.CamN = cnt.ToString();            // 顺带把 CamN 回写到 General
        }

        private ModelConfig GetCurrentJob()
        {
            var name = CboJob.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name) || _config?.Models == null) return null;
            return _config.Models.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private CameraConfig GetCurrentCam()
        {
            var job = GetCurrentJob();
            var camName = CboCam.SelectedItem as string;
            if (job?.Cameras == null || string.IsNullOrWhiteSpace(camName)) return null;
            return job.Cameras.FirstOrDefault(c => string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Grid

        private void BuildGridColumns()
        {
            if (_gridBuilt) return;
            _gridBuilt = true;

            dataGridView1.Columns.Clear();

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Job",
                DataPropertyName = nameof(PosGeneralRow.Model),
                ReadOnly = true,
                Width = 120
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Cam",
                DataPropertyName = nameof(PosGeneralRow.Camera),
                ReadOnly = true,
                Width = 100
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Pos",
                DataPropertyName = nameof(PosGeneralRow.Position),
                Width = 120
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Exposure",
                DataPropertyName = nameof(PosGeneralRow.Exposure),
                Width = 90
            });

            // MainUsed
            var colMainUsed = new DataGridViewComboBoxColumn
            {
                HeaderText = "MainUsed",
                DataPropertyName = nameof(PosGeneralRow.MainUsed),
                Width = 90,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colMainUsed.Items.Add("0");
            colMainUsed.Items.Add("1");
            dataGridView1.Columns.Add(colMainUsed);

            // MainChannel
            var colMainCh = new DataGridViewComboBoxColumn
            {
                HeaderText = "MainChannel",
                DataPropertyName = nameof(PosGeneralRow.MainChannel),
                Width = 110,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colMainCh.Items.AddRange(new object[] { "0", "1", "2", "3" });
            dataGridView1.Columns.Add(colMainCh);

            // SecondUsed
            var colSecUsed = new DataGridViewComboBoxColumn
            {
                HeaderText = "SecondUsed",
                DataPropertyName = nameof(PosGeneralRow.SecondUsed),
                Width = 100,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colSecUsed.Items.Add("0");
            colSecUsed.Items.Add("1");
            dataGridView1.Columns.Add(colSecUsed);

            // SecondChannel
            var colSecCh = new DataGridViewComboBoxColumn
            {
                HeaderText = "SecondChannel",
                DataPropertyName = nameof(PosGeneralRow.SecondChannel),
                Width = 120,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colSecCh.Items.AddRange(new object[] { "0", "1", "2", "3" });
            dataGridView1.Columns.Add(colSecCh);
        }

        private void RefreshGrid()
        {
            _rows.Clear();

            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            if (cam.Positions == null || cam.Positions.Count == 0)
            {
                // 没有任何 Pos，给一个占位行，便于用户新增
                _rows.Add(new PosGeneralRow
                {
                    Model = job.Name,
                    Camera = cam.Name,
                    Position = "Pos1",
                    Exposure = "10",
                    MainUsed = "1",
                    MainChannel = "0",
                    SecondUsed = "0",
                    SecondChannel = "0"
                });
                return;
            }

            // 把该 Cam 下所有 Pos 一次性列出来
            foreach (var pos in cam.Positions)
            {
                var g = pos.General ?? CreateDefaultPositionGeneral();
                _rows.Add(new PosGeneralRow
                {
                    Model = job.Name,
                    Camera = cam.Name,
                    Position = pos.Name ?? "",
                    Exposure = g.Exposure ?? "10",
                    MainUsed = g.MainUsed ?? "0",
                    MainChannel = g.MainChannel ?? "0",
                    SecondUsed = g.SecondUsed ?? "0",
                    SecondChannel = g.SecondChannel ?? "0"
                });
            }
        }

        #endregion

        #region 

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            // 自动生成一个不重复的 Pos 名
            var existing = new HashSet<string>(_rows.Select(r => r.Position ?? ""), StringComparer.OrdinalIgnoreCase);
            int n = 1;
            string next;
            do { next = "Pos" + n; n++; } while (existing.Contains(next));

            _rows.Add(new PosGeneralRow
            {
                Model = job.Name,
                Camera = cam.Name,
                Position = next,
                Exposure = "10",
                MainUsed = "0",
                MainChannel = "0",
                SecondUsed = "0",
                SecondChannel = "0"
            });

            if (_rows.Count > 0)
            {
                dataGridView1.ClearSelection();
                dataGridView1.Rows[_rows.Count - 1].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = _rows.Count - 1;
            }
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0) return;
            var toRemove = new List<PosGeneralRow>();
            foreach (DataGridViewRow r in dataGridView1.SelectedRows)
            {
                var item = r.DataBoundItem as PosGeneralRow;
                if (item != null) toRemove.Add(item);
            }
            foreach (var it in toRemove)
                _rows.Remove(it);
        }

        private void BtnAddCam_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            if (job == null) return;

            if (job.Cameras == null) job.Cameras = new List<CameraConfig>();

            // 生成一个唯一相机名：Cam1/Cam2/...（找最大编号+1）
            string newName = GenerateNextCamName(job);

            job.Cameras.Add(new CameraConfig
            {
                Name = newName,
                Positions = new List<PositionConfig>()   // 先空列表
            });

            // UI 刷新并选中新相机
            FillCams();
            CboCam.SelectedItem = newName;
            UpdateCamCountLabel();
            RefreshGrid();

            // 立刻落盘（可留到点“保存”时统一保存，二选一）
            XmlConfigHelper.Save(_config, _configPath);
            // MessageBox.Show($"已添加：{newName}");
        }

        private string GenerateNextCamName(ModelConfig job)
        {
            int maxNo = 0;
            foreach (var c in job.Cameras)
            {
                int n = TryParseCamIndex(c?.Name);
                if (n > maxNo) maxNo = n;
            }
            return "Cam" + (maxNo + 1);
        }

        private int TryParseCamIndex(string camName)
        {
            if (string.IsNullOrWhiteSpace(camName)) return 0;
            // Cam10, CAM2, cam003 都兼容
            if (camName.Length >= 4 &&
                camName.StartsWith("Cam", StringComparison.OrdinalIgnoreCase))
            {
                int n;
                if (int.TryParse(camName.Substring(3), out n)) return n;
            }
            return 0;
        }

        private void BtnDeleteCam_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            if (MessageBox.Show($"确定删除相机 {cam.Name} 及其所有 Pos？",
                "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            job.Cameras.Remove(cam);

            FillCams();                 // 重新填充下拉
            if (CboCam.Items.Count > 0) CboCam.SelectedIndex = 0;

            UpdateCamCountLabel();      // CamN 同步
            RefreshGrid();

            XmlConfigHelper.Save(_config, _configPath);
        }

        #endregion

        #region 保存

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            // 确保容器存在
            if (job.Cameras == null) job.Cameras = new List<CameraConfig>();
            if (cam.Positions == null) cam.Positions = new List<PositionConfig>();

            // 先把这个 Cam 的 Pos 全清空，再用表格内容重建（支持删行）
            cam.Positions.Clear();

            foreach (var r in _rows)
            {
                if (r == null) continue;
                var pos = new PositionConfig
                {
                    Name = string.IsNullOrWhiteSpace(r.Position) ? "Pos1" : r.Position,
                    General = new PositionGeneralConfig
                    {
                        Exposure = r.Exposure ?? "10",
                        MainUsed = r.MainUsed ?? "0",
                        MainChannel = r.MainChannel ?? "0",
                        SecondUsed = r.SecondUsed ?? "0",
                        SecondChannel = r.SecondChannel ?? "0"
                    }
                };
                cam.Positions.Add(pos);
            }

            // 回写 CamN（显示和存档都更新）
            UpdateCamCountLabel();

            // 落盘一次（确保只弹一次）
            XmlConfigHelper.Save(_config, _configPath);
            MessageBox.Show("Saved successfully！", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 通知外部（如果主窗体订阅了，确保只订阅一次）
            var h = ConfigSaved;
            if (h != null) h(this, EventArgs.Empty);
        }

        #endregion

        #region 辅助类型/默认值

        private static PositionGeneralConfig CreateDefaultPositionGeneral()
        {
            return new PositionGeneralConfig
            {
                Exposure = "10",
                MainUsed = "1",
                MainChannel = "0",
                SecondUsed = "0",
                SecondChannel = "0"
            };
        }

        private class PosGeneralRow
        {
            public string Model { get; set; }
            public string Camera { get; set; }
            public string Position { get; set; }
            public string Exposure { get; set; }
            public string MainUsed { get; set; }
            public string MainChannel { get; set; }
            public string SecondUsed { get; set; }
            public string SecondChannel { get; set; }
        }

        #endregion
    }
}
