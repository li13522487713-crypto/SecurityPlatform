#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer? components = null;
    private AntdUI.Label _titleLabel = null!;
    private AntdUI.Label _pwdLabel = null!;
    private AntdUI.Input _passwordBox = null!;
    private AntdUI.Label _statusLabel = null!;
    private AntdUI.Button _loginBtn = null!;
    private AntdUI.Button _initBtn = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _titleLabel = new AntdUI.Label();
        _pwdLabel = new AntdUI.Label();
        _passwordBox = new AntdUI.Input();
        _statusLabel = new AntdUI.Label();
        _loginBtn = new AntdUI.Button();
        _initBtn = new AntdUI.Button();
        SuspendLayout();
        // 
        // _titleLabel
        // 
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 134);
        _titleLabel.Location = new Point(20, 15);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(172, 31);
        _titleLabel.TabIndex = 0;
        _titleLabel.Text = "Atlas 证书颁发工具";
        // 
        // _pwdLabel
        // 
        _pwdLabel.AutoSize = true;
        _pwdLabel.Location = new Point(20, 55);
        _pwdLabel.Name = "_pwdLabel";
        _pwdLabel.Size = new Size(80, 24);
        _pwdLabel.TabIndex = 1;
        _pwdLabel.Text = "颁发密码：";
        // 
        // _passwordBox
        // 
        _passwordBox.Location = new Point(20, 78);
        _passwordBox.Name = "_passwordBox";
        _passwordBox.PasswordChar = '●';
        _passwordBox.Size = new Size(340, 40);
        _passwordBox.TabIndex = 2;
        _passwordBox.KeyDown += PasswordBox_KeyDown;
        // 
        // _statusLabel
        // 
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.Red;
        _statusLabel.Location = new Point(20, 122);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(0, 24);
        _statusLabel.TabIndex = 3;
        // 
        // _loginBtn
        // 
        _loginBtn.Location = new Point(220, 154);
        _loginBtn.Name = "_loginBtn";
        _loginBtn.Size = new Size(70, 32);
        _loginBtn.TabIndex = 4;
        _loginBtn.Text = "进入";
        _loginBtn.Click += LoginBtn_Click;
        // 
        // _initBtn
        // 
        _initBtn.Location = new Point(300, 154);
        _initBtn.Name = "_initBtn";
        _initBtn.Size = new Size(80, 32);
        _initBtn.TabIndex = 5;
        _initBtn.Text = "初始化密钥";
        _initBtn.Click += OnInitKey;
        // 
        // LoginForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 220);
        Controls.Add(_initBtn);
        Controls.Add(_loginBtn);
        Controls.Add(_statusLabel);
        Controls.Add(_passwordBox);
        Controls.Add(_pwdLabel);
        Controls.Add(_titleLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Atlas License Issuer — 验证颁发密码";
        ResumeLayout(false);
        PerformLayout();
    }
}
