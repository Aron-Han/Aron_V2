using Aron_V2;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Shapes;
using System.Xml.Linq;
//using WindowsFormsApp2.Database;

namespace WindowsFormsApp2
{
    public partial class DataBase : Form
    {
        private DateTime? startDate = null;
        private DateTime? endDate = null;
        private int totalRows = 0;
        private int totalPages = 0;
        private int pageSize = 50;
        private int currentPage = 1;
        public static long InsertInspectionRecord(
            string serial,
            string jobN,
            int channel,
            string camN,
            string posN,
            int resultCode,
            string rawMessage,
            IEnumerable<(string Name, string Type, string ValueText, int Start, int Length)> outputs
        )
        {
            EnsureSchema();

            using (var conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    // 1) 主表
                    long orderId;
                    using (var cmd = new SQLiteCommand(
                        @"INSERT INTO Orders (Serial, JobN, Channel, CamN, PosN, ResultCode, RawMessage, CreatedAt)
                          VALUES (@Serial,@JobN,@Channel,@CamN,@PosN,@ResultCode,@RawMessage, datetime('now','localtime'));
                          SELECT last_insert_rowid();", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Serial", serial ?? "");
                        cmd.Parameters.AddWithValue("@JobN", jobN ?? "");
                        cmd.Parameters.AddWithValue("@Channel", channel);
                        cmd.Parameters.AddWithValue("@CamN", camN ?? "");
                        cmd.Parameters.AddWithValue("@PosN", posN ?? "");
                        cmd.Parameters.AddWithValue("@ResultCode", resultCode);
                        cmd.Parameters.AddWithValue("@RawMessage", rawMessage ?? "");
                        orderId = (long)(cmd.ExecuteScalar() ?? 0L);
                    }

                    // 2) 明细
                    if (outputs != null)
                    {
                        foreach (var o in outputs)
                        {
                            using (var cmd = new SQLiteCommand(
                                @"INSERT INTO OrderOutputs (OrderId,Name,Type,ValueText,Channel,Start,Length)
                                  VALUES (@OrderId,@Name,@Type,@ValueText,@Channel,@Start,@Length)", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@OrderId", orderId);
                                cmd.Parameters.AddWithValue("@Name", o.Name ?? "");
                                cmd.Parameters.AddWithValue("@Type", o.Type ?? "string");
                                cmd.Parameters.AddWithValue("@ValueText", o.ValueText ?? "");
                                cmd.Parameters.AddWithValue("@Channel", channel);
                                cmd.Parameters.AddWithValue("@Start", o.Start);
                                cmd.Parameters.AddWithValue("@Length", o.Length);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    tx.Commit();
                    return orderId;
                }
            }
        }

        public DataBase()
        {
            InitializeComponent();

            DbMigrator.EnsureDb(Global.dBPath);

            // 表结构保障
            EnsureSchema();

            // DataGridView 基本外观
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.CellDoubleClick += dataGridView1_CellDoubleClick;

            // 时间控件格式
            dateTimePicker1.Format = DateTimePickerFormat.Custom;
            dateTimePicker2.Format = DateTimePickerFormat.Custom;
            dateTimePicker1.CustomFormat = "yyyy-MM-dd HH:mm:ss";
            dateTimePicker2.CustomFormat = "yyyy-MM-dd HH:mm:ss";

            // 先加载统计与第一页
            LoadTotalCount();
            LoadPage(currentPage);
        }

        private static void EnsureSchema()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Global.dBPath) ?? AppDomain.CurrentDomain.BaseDirectory);

            using (var conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();

                // Orders
                using (var cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Serial      TEXT,
    JobN        TEXT,        -- 新命名
    Channel     INTEGER,
    CamN        TEXT,        -- 新命名
    PosN        TEXT,        -- 新命名
    ResultCode  INTEGER,     -- 新命名
    RawMessage  TEXT,        -- 新命名
    CreatedAt   TEXT
);", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 逐列补齐（老库可能没有这些新列）
                EnsureColumn(conn, "Orders", "JobN", "TEXT");
                EnsureColumn(conn, "Orders", "CamN", "TEXT");
                EnsureColumn(conn, "Orders", "PosN", "TEXT");
                EnsureColumn(conn, "Orders", "ResultCode", "INTEGER");
                EnsureColumn(conn, "Orders", "RawMessage", "TEXT");
                EnsureColumn(conn, "Orders", "Channel", "INTEGER");
                EnsureColumn(conn, "Orders", "Serial", "TEXT");
                EnsureColumn(conn, "Orders", "CreatedAt", "TEXT");

                // OrderOutputs
                using (var cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS OrderOutputs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId   INTEGER,
    Name      TEXT,
    Type      TEXT,
    ValueText TEXT,
    Channel   INTEGER,
    Start     INTEGER,
    Length    INTEGER,
    FOREIGN KEY(OrderId) REFERENCES Orders(Id)
);", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 可选：做一个兼容视图，给还在用旧列名(Job/Cam/Pos/Result/Payload)的查询临时过渡
                using (var cmd = new SQLiteCommand(@"
DROP VIEW IF EXISTS OrdersCompat;
CREATE VIEW OrdersCompat AS
SELECT
  Id, Serial,
  JobN  AS Job,
  CamN  AS Cam,
  PosN  AS Pos,
  ResultCode AS Result,
  RawMessage AS Payload,
  Channel, CreatedAt
FROM Orders;", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ClearDatabaseData()
        {
            using (var conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    new SQLiteCommand("DELETE FROM OrderOutputs;", conn, tx).ExecuteNonQuery();
                    new SQLiteCommand("DELETE FROM Orders;", conn, tx).ExecuteNonQuery();
                    new SQLiteCommand("DELETE FROM sqlite_sequence WHERE name IN ('Orders','OrderOutputs');", conn, tx).ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        // ====== 统计 ======
        private void LoadTotalCount()
        {
            using (SQLiteConnection conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM Orders WHERE 1=1";
                if (startDate.HasValue) sql += " AND CreatedAt >= @StartDate";
                if (endDate.HasValue) sql += " AND CreatedAt <= @EndDate";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (startDate.HasValue)
                        cmd.Parameters.AddWithValue("@StartDate", startDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (endDate.HasValue)
                        cmd.Parameters.AddWithValue("@EndDate", endDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));

                    totalRows = Convert.ToInt32(cmd.ExecuteScalar());
                    totalPages = (int)Math.Ceiling((double)totalRows / pageSize);
                }
            }
        }

        // ====== 分页加载 ======
        private async void LoadPage(int page)
        {
            currentPage = Math.Max(1, Math.Min(page, totalPages));
            label3.Text = $"正在加载第 {currentPage}/{totalPages} 页...";

            DataTable dt = await System.Threading.Tasks.Task.Run(() => GetPageData(currentPage));
            BindDataTableToGrid(dt);

            label3.Text = $"第 {currentPage}/{totalPages} 页";

        }

        private DataTable GetPageData(int page)
        {
            int offset = (page - 1) * pageSize;
            DataTable dt = new DataTable();

            using (SQLiteConnection conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();

                string whereTime = "";
                if (startDate.HasValue) whereTime += " AND CreatedAt >= @StartDate";
                if (endDate.HasValue) whereTime += " AND CreatedAt <= @EndDate";

                // 用兼容视图 OrdersCompat：它把 JobN/CamN/PosN/ResultCode/RawMessage 投影为 Job/Cam/Pos/Result/Payload
                string sql = string.Format(@"
SELECT 
  Id,
  Serial,
  Result,          
  Job,             
  Cam,             
  Pos  AS PosN,    
  Channel,
  CreatedAt,
  Payload          
FROM OrdersCompat
WHERE 1=1 {0}
ORDER BY datetime(CreatedAt) DESC
LIMIT @PageSize OFFSET @Offset;", whereTime);

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (startDate.HasValue)
                        cmd.Parameters.AddWithValue("@StartDate", startDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (endDate.HasValue)
                        cmd.Parameters.AddWithValue("@EndDate", endDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));

                    cmd.Parameters.AddWithValue("@PageSize", pageSize);
                    cmd.Parameters.AddWithValue("@Offset", offset);

                    new SQLiteDataAdapter(cmd).Fill(dt);
                }
            }
            return dt;
        }

        // ====== 过滤（时间） ======
        private void Filter_Click(object sender, EventArgs e)
        {
            startDate = dateTimePicker1.Value;
            endDate = dateTimePicker2.Value;

            currentPage = 1;
            LoadTotalCount();
            LoadPage(currentPage);
        }

        // ====== 下一页 ======
        private void button1_Click(object sender, EventArgs e)
        {
            if (currentPage < totalPages) LoadPage(currentPage + 1);
        }

        // ====== 上一页 ======
        private void button4_Click(object sender, EventArgs e)
        {
            if (currentPage > 1) LoadPage(currentPage - 1);
        }

        // ====== 导出 CSV（主表） ======
        private void button2_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count == 0)
            {
                MessageBox.Show("Export Failed！");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV文件|*.csv";
                sfd.FileName = $"Export_{DateTime.Now:yyyyMMddHHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ExportDataGridViewToCsv(dataGridView1, sfd.FileName);
                    MessageBox.Show("Export Successed！");
                }
            }
        }

        private void ExportDataGridViewToCsv(DataGridView dgv, string filePath)
        {
            var sb = new StringBuilder();

            // 列头
            for (int i = 0; i < dgv.Columns.Count; i++)
            {
                sb.Append(dgv.Columns[i].HeaderText);
                if (i < dgv.Columns.Count - 1) sb.Append(",");
            }
            sb.AppendLine();

            // 行
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                for (int i = 0; i < dgv.Columns.Count; i++)
                {
                    string value = row.Cells[i].Value?.ToString() ?? "";
                    if (value.Contains(",") || value.Contains("\""))
                        value = "\"" + value.Replace("\"", "\"\"") + "\"";
                    sb.Append(value);
                    if (i < dgv.Columns.Count - 1) sb.Append(",");
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // ====== 清空 ======
        private void ClearAll_Click(object sender, EventArgs e)
        {
            ClearDatabaseData();
            LoadTotalCount();
            LoadPage(1);
            MessageBox.Show("Data has been cleared!");
        }

        // ====== 双击行查看该 Order 的全部输出项 ======
        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dataGridView1.Rows[e.RowIndex];
            if (row == null) return;

            if (!long.TryParse(row.Cells["Id"]?.Value?.ToString() ?? "", out long orderId)) return;

            var outputs = LoadOutputsByOrder(orderId);
            ShowOutputsDialog(orderId, outputs);
        }

        private List<(string Name, string Type, string ValueText, int Start, int Length)> LoadOutputsByOrder(long orderId)
        {
            var list = new List<(string, string, string, int, int)>();
            using (var conn = new SQLiteConnection($"Data Source={Global.dBPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                    @"SELECT Name,Type,ValueText,Start,Length 
                      FROM OrderOutputs WHERE OrderId=@Id ORDER BY Start", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", orderId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add((
                                rd["Name"]?.ToString() ?? "",
                                rd["Type"]?.ToString() ?? "string",
                                rd["ValueText"]?.ToString() ?? "",
                                Convert.ToInt32(rd["Start"]),
                                Convert.ToInt32(rd["Length"])
                            ));
                        }
                    }
                }
            }
            return list;
        }

        private void ShowOutputsDialog(long orderId, List<(string Name, string Type, string ValueText, int Start, int Length)> outputs)
        {
            var f = new Form
            {
                Text = $"Outputs - Order #{orderId}",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(800, 500)
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "Type", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", DataPropertyName = "ValueText", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Start", DataPropertyName = "Start", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Length", DataPropertyName = "Length", Width = 70 });

            grid.DataSource = outputs
                .Select(o => new { o.Name, o.Type, o.ValueText, o.Start, o.Length })
                .ToList();

            f.Controls.Add(grid);
            f.ShowDialog(this);
        }

        // ====== 便捷：测试插入一条（示例按钮，不需要可删掉） ======
        private void button3_Click(object sender, EventArgs e)
        {
            var outputs = new List<(string Name, string Type, string ValueText, int Start, int Length)>
            {
                ("Result","short","1", 0,2),
                ("Offset_X","float","1.23", 10,4),
                ("Offset_Y","float","-0.45", 14,4),
                ("Message","string","hello", 100,10)
            };

            InsertInspectionRecord(
                serial: "SN123",
                jobN: "Job1",
                channel: 0,
                camN: "Cam1",
                posN: "Pos1",
                resultCode: 1,
                rawMessage: "",
                outputs: outputs
            );

            LoadTotalCount();
            LoadPage(currentPage);
        }

        static void EnsureColumn(SQLiteConnection conn, string table, string column, string type)
        {
            using (var check = new SQLiteCommand($"PRAGMA table_info({table});", conn))
            using (var r = check.ExecuteReader())
            {
                bool exists = false;
                while (r.Read())
                {
                    var name = Convert.ToString(r["name"]);
                    if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true; break;
                    }
                }
                if (!exists)
                {
                    using (var add = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {type};", conn))
                    {
                        add.ExecuteNonQuery();
                    }
                }
            }
        }


        private void BindDataTableToGrid(DataTable dt)
        {
            // 彻底清理旧列，避免早先的未绑定列遮挡
            dataGridView1.DataSource = null;
            dataGridView1.Columns.Clear();

            // 自动生成列
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.VirtualMode = false;             // 确保不是虚拟模式
            dataGridView1.ReadOnly = true;                 // 纯查看更稳
            dataGridView1.EditMode = DataGridViewEditMode.EditProgrammatically;

            dataGridView1.DataSource = dt;

            // 可选：自适应
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dataGridView1.AutoResizeColumns();

            // 强制刷新
            dataGridView1.Invalidate();
        }

    }

    public static class DbMigrator
    {
        public static void EnsureDb(string dbPath)
        {
            // 确保目录存在（注意之前你的 Path 二义性：这里显式用 System.IO.Path）
            string dir = System.IO.Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dbPath)))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    // 1) 表不存在则创建“新架构”
                    using (var cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS Orders(
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Serial    TEXT,              -- SN/条码
    Result    TEXT,              -- OK/NG 或码
    Result_X  REAL,
    Result_Y  REAL,
    Result_A  REAL,
    Job       TEXT,              -- Job 名（如 Job1）
    Cam       TEXT,              -- 相机名（如 Cam1）
    Pos       TEXT,              -- 工位（如 Pos1）
    Channel   INTEGER DEFAULT 0, -- 0..3
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    Payload   TEXT               -- 可选：JSON 扩展字段
);", conn, tx))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 2) 老库补列（SQLite 没有 IF NOT EXISTS 列，先查现有列名）
                    var existing = GetColumns(conn, tx, "Orders");
                    string[] needCols = new string[]
                    {
                    "Serial","Result","Result_X","Result_Y","Result_A",
                    "Job","Cam","Pos","Channel","CreatedAt","Payload"
                    };

                    var missing = new List<string>();
                    foreach (var col in needCols)
                    {
                        bool has = false;
                        for (int i = 0; i < existing.Count; i++)
                            if (string.Equals(existing[i], col, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
                        if (!has) missing.Add(col);
                    }

                    foreach (var col in missing)
                    {
                        string ddl;
                        if (col == "Result_X" || col == "Result_Y" || col == "Result_A")
                            ddl = "ALTER TABLE Orders ADD COLUMN " + col + " REAL;";
                        else if (col == "Channel")
                            ddl = "ALTER TABLE Orders ADD COLUMN Channel INTEGER DEFAULT 0;";
                        else if (col == "CreatedAt")
                            ddl = "ALTER TABLE Orders ADD COLUMN CreatedAt TEXT DEFAULT (datetime('now','localtime'));";
                        else if (col == "Payload")
                            ddl = "ALTER TABLE Orders ADD COLUMN Payload TEXT;";
                        else
                            ddl = "ALTER TABLE Orders ADD COLUMN " + col + " TEXT;";

                        using (var cmd = new SQLiteCommand(ddl, conn, tx))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 3) 索引（存在相关列再建）
                    ExecSafe(conn, tx, "CREATE INDEX IF NOT EXISTS idx_orders_created ON Orders(CreatedAt);");
                    if (HasAll(existing, "Job", "Cam", "Channel", "CreatedAt"))
                    {
                        ExecSafe(conn, tx, "CREATE INDEX IF NOT EXISTS idx_orders_filters ON Orders(Job, Cam, Channel, CreatedAt);");
                    }

                    tx.Commit();
                }
            }
        }

        private static List<string> GetColumns(SQLiteConnection conn, SQLiteTransaction tx, string table)
        {
            var cols = new List<string>();
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ");", conn, tx))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    cols.Add(Convert.ToString(r["name"]));
                }
            }
            return cols;
        }

        private static void ExecSafe(SQLiteConnection conn, SQLiteTransaction tx, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn, tx))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static bool HasAll(List<string> existing, params string[] cols)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < existing.Count; j++)
                {
                    if (string.Equals(existing[j], cols[i], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true; break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}
