#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class InitKeyForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label _pwdLabel = null!;
    private Label _confirmLabel = null!;
    private AntdUI.Input _passwordBox = null!;
    private AntdUI.Input _confirmBox = null!;
    private AntdUI.Label _statusLabel = null!;
    private AntdUI.Button _okBtn = null!;
    private AntdUI.Button _cancelBtn = null!;

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
        _pwdLabel = new Label();
        _confirmLabel = new Label();
        _passwordBox = new AntdUI.Input();
        _confirmBox = new AntdUI.Input();
        _statusLabel = new AntdUI.Label();
        _okBtn = new AntdUI.Button();
        _cancelBtn = new AntdUI.Button();
        SuspendLayout();
        // 
        // _pwdLabel
        // 
        _pwdLabel.AutoSize = true;
        _pwdLabel.Location = new Point(20, 20);
        _pwdLabel.Name = "_pwdLabel";
        _pwdLabel.Size = new Size(250, 24);
        _pwdLabel.TabIndex = 0;
        _pwdLabel.Text = "设置颁发密码（建议16位以上）：";
        // 
        // _confirmLabel
        // 
        _confirmLabel.AutoSize = true;
        _confirmLabel.Location = new Point(20, 96);
        _confirmLabel.Name = "_confirmLabel";
        _confirmLabel.Size = new Size(82, 24);
        _confirmLabel.TabIndex = 2;
        _confirmLabel.Text = "确认密码：";
        // 
        // _passwordBox
        // 
        _passwordBox.Location = new Point(20, 47);
        _passwordBox.Name = "_passwordBox";
        _passwordBox.PasswordChar = '●';
        _passwordBox.Size = new Size(330, 40);
        _passwordBox.TabIndex = 1;
        // 
        // _confirmBox
        // 
        _confirmBox.Location = new Point(20, 123);
        _confirmBox.Name = "_confirmBox";
        _confirmBox.PasswordChar = '●';
        _confirmBox.Size = new Size(330, 40);
        _confirmBox.TabIndex = 3;
        // 
        // _statusLabel
        // 
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.Red;
        _statusLabel.Location = new Point(20, 167);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(0, 24);
        _statusLabel.TabIndex = 4;
        // 
        // _okBtn
        // 
        _okBtn.Location = new Point(202, 194);
        _okBtn.Name = "_okBtn";
        _okBtn.Size = new Size(92, 32);
        _okBtn.TabIndex = 5;
        _okBtn.Text = "生成密钥对";
        _okBtn.Click += OkBtn_Click;
        // 
        // _cancelBtn
        // 
        _cancelBtn.Location = new Point(300, 194);
        _cancelBtn.Name = "_cancelBtn";
        _cancelBtn.Size = new Size(60, 32);
        _cancelBtn.TabIndex = 6;
        _cancelBtn.Text = "取消";
        _cancelBtn.Click += CancelBtn_Click;
        // 
        // InitKeyForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(380, 240);
        Controls.Add(_cancelBtn);
        Controls.Add(_okBtn);
        Controls.Add(_statusLabel);
        Controls.Add(_confirmBox);
        Controls.Add(_passwordBox);
        Controls.Add(_confirmLabel);
        Controls.Add(_pwdLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "InitKeyForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "初始化颁发密钥对";
        ResumeLayout(false);
        PerformLayout();
    }
}
