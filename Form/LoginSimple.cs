using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Aron_V2.Security; // UserStore / UserRecord

namespace Aron_V2
{
	public sealed class LoginSimple : Form
	{
		// --- 登录结果（主窗体可读取） ---
		public UserRecord LoggedInUser { get; private set; }

		// --- UI ---
		private ComboBox cboUsers;
		private TextBox txtPwd;
		private Button btnLogin, btnChangePwd, btnRegister, btnDelete, btnCancel;
		private Label lblInfo;

		public LoginSimple()
		{
			// 基本窗体
			Text = "Login";
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ConfigureDialog(this, 480);
			KeyPreview = true;

			BuildUI();
			WireEvents();
			LoadUsersToCombo();
		}

		private void BuildUI()
		{
			var layout = CreateFieldLayout(480);
			var heading = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16), WrapContents = false };
			var userImage = DesktopTheme.CreateIcon(DesktopIcon.User, (int)(40 * DesktopTheme.DisplayScale), DesktopTheme.Accent);
			var picture = new PictureBox { Image = userImage, Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 12, 0) };
			picture.Disposed += (s, e) => userImage.Dispose();
			heading.Controls.Add(picture);
			var headingFont = new Font("Segoe UI", 18F);
			var headingLabel = new Label { Text = "User Login", AutoSize = true, Font = headingFont, Margin = new Padding(0, 4, 0, 0), ForeColor = DesktopTheme.Ink };
			headingLabel.Disposed += (s, e) => headingFont.Dispose();
			heading.Controls.Add(headingLabel);
			AddFullRow(layout, heading);
			cboUsers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
			txtPwd = new TextBox { UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle };
			AddField(layout, "User Name", cboUsers);
			AddField(layout, "Password", txtPwd);
			btnLogin = DialogButton("Login");
			btnChangePwd = DialogButton("Change Password");
			btnRegister = DialogButton("Register");
			btnDelete = DialogButton("Delete");
			btnCancel = DialogButton("Cancel", DialogResult.Cancel);
			DesktopTheme.StyleButton(btnLogin, true);
			btnDelete.ForeColor = Color.FromArgb(185, 45, 53);
			lblInfo = new Label { AutoSize = true, Visible = false, ForeColor = Color.FromArgb(185, 45, 53), Margin = new Padding(0, 4, 0, 4), MaximumSize = new Size(420, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right };
			lblInfo.TextChanged += (s, e) => lblInfo.Visible = !string.IsNullOrEmpty(lblInfo.Text);
			AddFullRow(layout, lblInfo);
			AddFullRow(layout, new Panel { Height = 1, BackColor = DesktopTheme.Border, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 16) });
			var secondary = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = Padding.Empty };
			secondary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			Button[] secondaryButtons = { btnChangePwd, btnRegister, btnDelete };
			for (int i = 0; i < secondaryButtons.Length; i++)
			{
				// Size the longer caption independently instead of making all
				// three columns as wide as Change Password.
				secondary.ColumnStyles.Add(i == 0 ? new ColumnStyle(SizeType.AutoSize) : new ColumnStyle(SizeType.Percent, 50F));
				secondaryButtons[i].Dock = DockStyle.Fill;
				secondaryButtons[i].Margin = new Padding(0, 0, i == 2 ? 0 : 10, 0);
				secondary.Controls.Add(secondaryButtons[i], i, 0);
			}
			AddFullRow(layout, secondary);
			var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, 20, 0, 0) };
			btnLogin.MinimumSize = btnCancel.MinimumSize = new Size(112, 36);
			btnLogin.Margin = Padding.Empty;
			btnCancel.Margin = new Padding(0, 0, 10, 0);
			actions.Controls.Add(btnLogin); actions.Controls.Add(btnCancel);
			AddFullRow(layout, actions);
			cboUsers.TabIndex = 0; txtPwd.TabIndex = 1;
			btnLogin.TabIndex = 0; btnCancel.TabIndex = 1;
			Controls.Add(layout);
			AcceptButton = btnLogin;
			CancelButton = btnCancel;
			ResumeLayout(true);
		}

		private static void ConfigureDialog(Form form, int width)
		{
			form.SuspendLayout();
			form.AutoScaleDimensions = new SizeF(96F, 96F);
			form.AutoScaleMode = AutoScaleMode.Dpi;
			var dialogFont = new Font("Segoe UI", 10F);
			form.Font = dialogFont;
			form.Disposed += (s, e) => dialogFont.Dispose();
			form.BackColor = Color.White;
			form.ForeColor = DesktopTheme.Ink;
			form.AutoSize = true;
			form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			form.ClientSize = new Size(width, 160);
		}

		private static TableLayoutPanel CreateFieldLayout(int width = 540)
		{
			var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(width, 0), Padding = new Padding(24), ColumnCount = 2, RowCount = 0 };
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			return layout;
		}

		private static void AddField(TableLayoutPanel layout, string text, Control editor)
		{
			int row = layout.RowCount++;
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 24, 10) }, 0, row);
			editor.MinimumSize = new Size(240, 0);
			editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			editor.Margin = new Padding(0, 10, 0, 10);
			layout.Controls.Add(editor, 1, row);
		}

		private static Button DialogButton(string text, DialogResult result = DialogResult.None)
		{
			var button = new Button { Text = text, DialogResult = result, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(104, 36), Padding = new Padding(12, 5, 12, 5), Margin = new Padding(6) };
			DesktopTheme.StyleButton(button);
			return button;
		}

		private static void AddFullRow(TableLayoutPanel layout, Control control)
		{
			int row = layout.RowCount++;
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.Controls.Add(control, 0, row);
			layout.SetColumnSpan(control, 2);
		}

		private static void AddButtons(TableLayoutPanel layout, params Button[] buttons)
		{
			var panel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
			panel.Controls.AddRange(buttons);
			AddFullRow(layout, panel);
		}

		private void WireEvents()
		{
			btnLogin.Click += (s, e) => DoLogin();
			btnChangePwd.Click += (s, e) => DoChangePassword();
			btnRegister.Click += (s, e) => DoRegister();
			btnDelete.Click += (s, e) => DoDelete();

			KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Escape) Close();
				// AcceptButton handles Enter once; do not also invoke PerformClick.
			};
		}

		private void LoadUsersToCombo()
		{
			try
			{
				var store = UserStore.Load();
				var names = store.Users?.Select(u => u.Username).OrderBy(x => x).ToArray() ?? Array.Empty<string>();
				cboUsers.Items.Clear();
				foreach (var n in names) cboUsers.Items.Add(n);
				if (cboUsers.Items.Count > 0) cboUsers.SelectedIndex = 0;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Login Failed：" + ex.Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void DoLogin()
		{
			lblInfo.Text = "";
			var user = cboUsers.SelectedItem as string;
			var pwd = txtPwd.Text;

			if (string.IsNullOrWhiteSpace(user))
			{
				lblInfo.Text = "Please Select User";
				return;
			}

			try
			{
				var store = UserStore.Load();
				if (UserStore.VerifyLogin(store, user, pwd, out var rec))
				{
					LoggedInUser = rec;
					DialogResult = DialogResult.OK;
					Close();
				}
				else
				{
					lblInfo.Text = "Account or password error";
				}
			}
			catch (Exception ex)
			{
				lblInfo.Text = "Login Failed：" + ex.Message;
			}
		}

		private void DoChangePassword()
		{
			var user = cboUsers.SelectedItem as string;
			if (string.IsNullOrWhiteSpace(user))
			{
				MessageBox.Show("Please select the username to change the password first", "Warning",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			using (var dlg = new ChangePasswordForm(user))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;

				try
				{
					var store = UserStore.Load();
					if (UserStore.ChangePassword(store, user, dlg.OldPassword, dlg.NewPassword, out var err))
					{
						UserStore.Save(store);
						MessageBox.Show("Password changed successfully", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
					else
					{
						MessageBox.Show("Password changed failed：" + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show("Change failed：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		// —— 注册新用户（需要 Admin 授权）——
		private void DoRegister()
		{
			if (!RequireAdmin()) return; // 先验证管理员

			using (var dlg = new RegisterUserForm())
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;

				try
				{
					var store = UserStore.Load();
					if (UserStore.AddUser(store, dlg.Username, dlg.Password, dlg.Role, out var err))
					{
						UserStore.Save(store);
						MessageBox.Show("registered successfully", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
						LoadUsersToCombo();
						var idx = cboUsers.Items.IndexOf(dlg.Username);
						if (idx >= 0) cboUsers.SelectedIndex = idx;
					}
					else
					{
						MessageBox.Show("registered failed：" + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show("registered failed：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		// —— 删除用户（需要 Admin 授权；禁止删除最后一个 Admin）——
		private void DoDelete()
		{
			var target = cboUsers.SelectedItem as string;
			if (string.IsNullOrWhiteSpace(target))
			{
				MessageBox.Show("Please select the user to delete", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (MessageBox.Show($"Make sure Delete user：{target}？", "OK",
					MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
				return;

			if (!RequireAdmin()) return; // 先验证管理员

			try
			{
				var store = UserStore.Load();
				if (UserStore.DeleteUser(store, target, out var err))
				{
					UserStore.Save(store);
					MessageBox.Show("Delete successfully", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
					LoadUsersToCombo();
				}
				else
				{
					MessageBox.Show("Delete failed：" + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Delete failed：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// —— 管理员认证弹窗 —— 
		private bool RequireAdmin()
		{
			using (var auth = new AdminAuthDialog())
			{
				if (auth.ShowDialog(this) != DialogResult.OK) return false;

				var store = UserStore.Load();
				if (!UserStore.VerifyLogin(store, auth.AdminUsername, auth.AdminPassword, out var admin))
				{
					MessageBox.Show("Administrator account or password error", "Insufficient permissions", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}
				if (!UserStore.IsAdmin(admin))
				{
					MessageBox.Show("This account is not an administrator", "Insufficient permissions", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}
				return true;
			}
		}

		// ==================== 内部对话框们 ====================

		// 1) 管理员认证
		private sealed class AdminAuthDialog : Form
		{
			public string AdminUsername => txtU.Text.Trim();
			public string AdminPassword => txtP.Text;

			private TextBox txtU, txtP;

			public AdminAuthDialog()
			{
				Text = "Administrator Certification";
				FormBorderStyle = FormBorderStyle.FixedDialog;
				StartPosition = FormStartPosition.CenterParent;
				MaximizeBox = MinimizeBox = false;
				ConfigureDialog(this, 540);
				var layout = CreateFieldLayout();
				txtU = new TextBox();
				txtP = new TextBox { UseSystemPasswordChar = true };
				AddField(layout, "Administrator account:", txtU);
				AddField(layout, "Administrator password:", txtP);
				var ok = DialogButton("OK", DialogResult.OK);
				var cancel = DialogButton("Cancel", DialogResult.Cancel);
				AddButtons(layout, ok, cancel);
				Controls.Add(layout);
				AcceptButton = ok; CancelButton = cancel;
				ResumeLayout(true);
			}
		}

		private sealed class ChangePasswordForm : Form
		{
			public string OldPassword => txtOld.Text;
			public string NewPassword => txtNew.Text;

			private TextBox txtOld, txtNew, txtNew2;

			public ChangePasswordForm(string username)
			{
				Text = $"Change password - {username}";
				StartPosition = FormStartPosition.CenterParent;
				FormBorderStyle = FormBorderStyle.FixedDialog;
				MaximizeBox = MinimizeBox = false;
				ConfigureDialog(this, 540);
				var layout = CreateFieldLayout();
				txtOld = new TextBox { UseSystemPasswordChar = true };
				txtNew = new TextBox { UseSystemPasswordChar = true };
				txtNew2 = new TextBox { UseSystemPasswordChar = true };
				AddField(layout, "Old password:", txtOld);
				AddField(layout, "New password:", txtNew);
				AddField(layout, "Confirm new password:", txtNew2);
				var ok = DialogButton("OK", DialogResult.OK);
				var cancel = DialogButton("Cancel", DialogResult.Cancel);

				ok.Click += (s, e) =>
				{
					if (txtNew.Text != txtNew2.Text)
					{
						MessageBox.Show("Two new passwords do not match。", "Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
						this.DialogResult = DialogResult.None;
					}
				};

				AddButtons(layout, ok, cancel);
				Controls.Add(layout);
				AcceptButton = ok;
				CancelButton = cancel;
				ResumeLayout(true);
			}
		}

		private sealed class RegisterUserForm : Form
		{
			public string Username => txtUser.Text.Trim();
			public string Password => txtPwd.Text;
			public string Role => (cboRole.SelectedItem as string) ?? "Operator";

			private TextBox txtUser, txtPwd, txtPwd2;
			private ComboBox cboRole;

			public RegisterUserForm()
			{
				Text = "Register a new user";
				StartPosition = FormStartPosition.CenterParent;
				FormBorderStyle = FormBorderStyle.FixedDialog;
				MaximizeBox = MinimizeBox = false;
				ConfigureDialog(this, 520);
				var layout = CreateFieldLayout();
				txtUser = new TextBox();
				txtPwd = new TextBox { UseSystemPasswordChar = true };
				txtPwd2 = new TextBox { UseSystemPasswordChar = true };
				cboRole = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
				AddField(layout, "Username:", txtUser);
				AddField(layout, "Password:", txtPwd);
				AddField(layout, "Confirm password:", txtPwd2);
				AddField(layout, "Role:", cboRole);
				cboRole.Items.AddRange(new object[] { "Admin", "Engineer", "Operator", "Guest" });
				cboRole.SelectedIndex = 2; // 默认 Operator

				var ok = DialogButton("Register", DialogResult.OK);
				var cancel = DialogButton("Cancel", DialogResult.Cancel);

				ok.Click += (s, e) =>
				{
					if (string.IsNullOrWhiteSpace(txtUser.Text))
					{
						MessageBox.Show("The username cannot be empty。", "Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
						this.DialogResult = DialogResult.None;
						return;
					}
					if (txtPwd.Text != txtPwd2.Text)
					{
						MessageBox.Show("Two passwords are inconsistent。", "Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
						this.DialogResult = DialogResult.None;
						return;
					}
				};

				AddButtons(layout, ok, cancel);
				Controls.Add(layout);
				AcceptButton = ok;
				CancelButton = cancel;
				ResumeLayout(true);
			}
		}
	}
}
