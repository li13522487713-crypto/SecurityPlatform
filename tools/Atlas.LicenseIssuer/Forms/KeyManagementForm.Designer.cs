#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class KeyManagementForm
{
    private System.ComponentModel.IContainer? components = null;
    private FlowLayoutPanel _panel = null!;
    private Label _titleLabel = null!;
    private AntdUI.Input _pubkeyBox = null!;
    private FlowLayoutPanel _btnRow = null!;
    private AntdUI.Button _copyBtn = null!;
    private AntdUI.Button _regenBtn = null!;
    private AntdUI.Label _tipLabel = null!;

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
        _panel = new FlowLayoutPanel();
        _titleLabel = new Label();
        _pubkeyBox = new AntdUI.Input();
        _btnRow = new FlowLayoutPanel();
        _copyBtn = new AntdUI.Button();
        _regenBtn = new AntdUI.Button();
        _tipLabel = new AntdUI.Label();
        _panel.SuspendLayout();
        _btnRow.SuspendLayout();
        SuspendLayout();
        // 
        // _panel
        // 
        _panel.Controls.Add(_titleLabel);
        _panel.Controls.Add(_pubkeyBox);
        _panel.Controls.Add(_btnRow);
        _panel.Controls.Add(_tipLabel);
        _panel.Dock = DockStyle.Fill;
        _panel.FlowDirection = FlowDirection.TopDown;
        _panel.Location = new Point(0, 0);
        _panel.Name = "_panel";
        _panel.Padding = new Padding(16);
        _panel.Size = new Size(620, 360);
        _panel.TabIndex = 0;
        _panel.WrapContents = false;
        // 
        // _titleLabel
        // 
        _titleLabel.AutoSize = true;
        _titleLabel.Location = new Point(19, 16);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(346, 24);
        _titleLabel.TabIndex = 0;
        _titleLabel.Text = "当前公钥（嵌入平台的 ECDSA P-256 公钥）：";
        // 
        // _pubkeyBox
        // 
        _pubkeyBox.Font = new Font("Consolas", 9F);
        _pubkeyBox.Location = new Point(19, 43);
        _pubkeyBox.Multiline = true;
        _pubkeyBox.Name = "_pubkeyBox";
        _pubkeyBox.ReadOnly = true;
        _pubkeyBox.Size = new Size(580, 180);
        _pubkeyBox.TabIndex = 1;
        // 
        // _btnRow
        // 
        _btnRow.AutoSize = true;
        _btnRow.Controls.Add(_copyBtn);
        _btnRow.Controls.Add(_regenBtn);
        _btnRow.Location = new Point(19, 229);
        _btnRow.Name = "_btnRow";
        _btnRow.Size = new Size(232, 40);
        _btnRow.TabIndex = 2;
        // 
        // _copyBtn
        // 
        _copyBtn.Location = new Point(3, 3);
        _copyBtn.Name = "_copyBtn";
        _copyBtn.Size = new Size(90, 34);
        _copyBtn.TabIndex = 0;
        _copyBtn.Text = "复制公钥";
        _copyBtn.Click += CopyBtn_Click;
        // 
        // _regenBtn
        // 
        _regenBtn.ForeColor = Color.DarkRed;
        _regenBtn.Location = new Point(99, 3);
        _regenBtn.Name = "_regenBtn";
        _regenBtn.Size = new Size(130, 34);
        _regenBtn.TabIndex = 1;
        _regenBtn.Text = "重新生成密钥对";
        _regenBtn.Click += RegenBtn_Click;
        // 
        // _tipLabel
        // 
        _tipLabel.AutoSize = true;
        _tipLabel.ForeColor = Color.DarkOrange;
        _tipLabel.Location = new Point(19, 272);
        _tipLabel.Name = "_tipLabel";
        _tipLabel.Size = new Size(611, 24);
        _tipLabel.TabIndex = 3;
        _tipLabel.Text = "⚠ 请将公钥交给平台方，嵌入 LicenseSignatureService.cs 中的 EmbeddedPublicKeyPem 常量。";
        // 
        // KeyManagementForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(620, 360);
        Controls.Add(_panel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "KeyManagementForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "密钥管理";
        _panel.ResumeLayout(false);
        _panel.PerformLayout();
        _btnRow.ResumeLayout(false);
        ResumeLayout(false);
    }
}
