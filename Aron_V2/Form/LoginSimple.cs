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
			ClientSize = new Size(450, 240);
			Font = new Font("Segoe UI", 9f);
			KeyPreview = true;

			BuildUI();
			WireEvents();
			LoadUsersToCombo();
		}

		private void BuildUI()
		{
			var lblUser = new Label { Text = "User Name：", AutoSize = true, Location = new Point(20, 25) };
			cboUsers = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Location = new Point(100, 20),
				Size = new Size(290, 26)
			};

			var lblPwd = new Label { Text = "Password：", AutoSize = true, Location = new Point(20, 65) };
			txtPwd = new TextBox
			{
				Location = new Point(100, 60),
				Size = new Size(290, 26),
				UseSystemPasswordChar = true
			};

			btnLogin = new Button
			{
				Text = "Login",
				Location = new Point(20, 110),
				Size = new Size(120, 30)
			};

			btnChangePwd = new Button
			{
				Text = "Change Password",
				Location = new Point(150, 110),
				Size = new Size(120, 30)
			};

			btnRegister = new Button
			{
				Text = "Register",
				Location = new Point(280, 110),
				Size = new Size(150, 30)
			};

			btnDelete = new Button
			{
				Text = "Delete",
				Location = new Point(20, 150),
				Size = new Size(180, 30)
			};

			btnCancel = new Button
			{
				Text = "Cancel",
				Location = new Point(320, 190),
				Size = new Size(90, 28),
				DialogResult = DialogResult.Cancel
			};

			lblInfo = new Label
			{
				AutoSize = false,
				Text = "",
				ForeColor = Color.FromArgb(200, 60, 60),
				Location = new Point(210, 150),
				Size = new Size(200, 30)
			};

			Controls.AddRange(new Control[]
			{
				lblUser, cboUsers, lblPwd, txtPwd,
				btnLogin, btnChangePwd, btnRegister, btnDelete, btnCancel, lblInfo
			});

			AcceptButton = btnLogin;
			CancelButton = btnCancel;
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
				if (e.KeyCode == Keys.Enter) btnLogin.PerformClick();
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
				ClientSize = new Size(340, 160);
				Font = new Font("Segoe UI", 9f);

				var lbl1 = new Label { Text = "administrator account：", Left = 20, Top = 25, AutoSize = true };
				var lbl2 = new Label { Text = "administrator password：", Left = 20, Top = 65, AutoSize = true };
				txtU = new TextBox { Left = 110, Top = 20, Width = 200 };
				txtP = new TextBox { Left = 110, Top = 60, Width = 200, UseSystemPasswordChar = true };

				var ok = new Button { Text = "OK", Left = 150, Top = 105, Width = 70, DialogResult = DialogResult.OK };
				var cancel = new Button { Text = "Cancel", Left = 240, Top = 105, Width = 70, DialogResult = DialogResult.Cancel };

				Controls.AddRange(new Control[] { lbl1, lbl2, txtU, txtP, ok, cancel });
				AcceptButton = ok; CancelButton = cancel;
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
				ClientSize = new Size(360, 180);
				Font = new Font("Segoe UI", 9f);

				var lbl1 = new Label { Text = "Old password：", AutoSize = true, Location = new Point(20, 25) };
				txtOld = new TextBox { Location = new Point(100, 20), Size = new Size(230, 26), UseSystemPasswordChar = true };

				var lbl2 = new Label { Text = "new password：", AutoSize = true, Location = new Point(20, 65) };
				txtNew = new TextBox { Location = new Point(100, 60), Size = new Size(230, 26), UseSystemPasswordChar = true };

				var lbl3 = new Label { Text = "Confirm new password：", AutoSize = true, Location = new Point(20, 105) };
				txtNew2 = new TextBox { Location = new Point(100, 100), Size = new Size(230, 26), UseSystemPasswordChar = true };

				var ok = new Button { Text = "确定", Location = new Point(160, 135), Size = new Size(80, 28), DialogResult = DialogResult.OK };
				var cancel = new Button { Text = "取消", Location = new Point(250, 135), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };

				ok.Click += (s, e) =>
				{
					if (txtNew.Text != txtNew2.Text)
					{
						MessageBox.Show("Two new passwords do not match。", "Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
						this.DialogResult = DialogResult.None;
					}
				};

				Controls.AddRange(new Control[] { lbl1, txtOld, lbl2, txtNew, lbl3, txtNew2, ok, cancel });
				AcceptButton = ok;
				CancelButton = cancel;
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
				ClientSize = new Size(400, 230);
				Font = new Font("Segoe UI", 9f);

				var lblU = new Label { Text = "Username：", AutoSize = true, Location = new Point(20, 25) };
				txtUser = new TextBox { Location = new Point(100, 20), Size = new Size(270, 26) };

				var lblP = new Label { Text = "Password：", AutoSize = true, Location = new Point(20, 65) };
				txtPwd = new TextBox { Location = new Point(100, 60), Size = new Size(270, 26), UseSystemPasswordChar = true };

				var lblP2 = new Label { Text = "Confirm Password：", AutoSize = true, Location = new Point(20, 105) };
				txtPwd2 = new TextBox { Location = new Point(100, 100), Size = new Size(270, 26), UseSystemPasswordChar = true };

				var lblR = new Label { Text = "Role：", AutoSize = true, Location = new Point(20, 145) };
				cboRole = new ComboBox
				{
					Location = new Point(100, 140),
					Size = new Size(270, 26),
					DropDownStyle = ComboBoxStyle.DropDownList
				};
				cboRole.Items.AddRange(new object[] { "Admin", "Engineer", "Operator", "Guest" });
				cboRole.SelectedIndex = 2; // 默认 Operator

				var ok = new Button { Text = "Register", Location = new Point(210, 175), Size = new Size(75, 30), DialogResult = DialogResult.OK };
				var cancel = new Button { Text = "Cancel", Location = new Point(295, 175), Size = new Size(75, 30), DialogResult = DialogResult.Cancel };

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

				Controls.AddRange(new Control[] { lblU, txtUser, lblP, txtPwd, lblP2, txtPwd2, lblR, cboRole, ok, cancel });
				AcceptButton = ok;
				CancelButton = cancel;
			}
		}
	}
}
