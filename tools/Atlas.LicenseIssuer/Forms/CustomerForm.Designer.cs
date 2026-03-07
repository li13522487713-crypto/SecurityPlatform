#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class CustomerForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label _nameLabel = null!;
    private AntdUI.Input _nameBox = null!;
    private Label _contactLabel = null!;
    private AntdUI.Input _contactBox = null!;
    private Label _remarkLabel = null!;
    private AntdUI.Input _remarkBox = null!;
    private AntdUI.Button _saveBtn = null!;
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
        _nameLabel = new Label();
        _nameBox = new AntdUI.Input();
        _contactLabel = new Label();
        _contactBox = new AntdUI.Input();
        _remarkLabel = new Label();
        _remarkBox = new AntdUI.Input();
        _saveBtn = new AntdUI.Button();
        _cancelBtn = new AntdUI.Button();
        SuspendLayout();
        // 
        // _nameLabel
        // 
        _nameLabel.AutoSize = true;
        _nameLabel.Location = new Point(20, 44);
        _nameLabel.Name = "_nameLabel";
        _nameLabel.Size = new Size(98, 24);
        _nameLabel.TabIndex = 0;
        _nameLabel.Text = "客户名称 *：";
        // 
        // _nameBox
        // 
        _nameBox.Location = new Point(20, 68);
        _nameBox.Name = "_nameBox";
        _nameBox.Size = new Size(340, 40);
        _nameBox.TabIndex = 1;
        // 
        // _contactLabel
        // 
        _contactLabel.AutoSize = true;
        _contactLabel.Location = new Point(20, 114);
        _contactLabel.Name = "_contactLabel";
        _contactLabel.Size = new Size(82, 24);
        _contactLabel.TabIndex = 2;
        _contactLabel.Text = "联系方式：";
        // 
        // _contactBox
        // 
        _contactBox.Location = new Point(20, 138);
        _contactBox.Name = "_contactBox";
        _contactBox.Size = new Size(340, 40);
        _contactBox.TabIndex = 3;
        // 
        // _remarkLabel
        // 
        _remarkLabel.AutoSize = true;
        _remarkLabel.Location = new Point(20, 184);
        _remarkLabel.Name = "_remarkLabel";
        _remarkLabel.Size = new Size(58, 24);
        _remarkLabel.TabIndex = 4;
        _remarkLabel.Text = "备注：";
        // 
        // _remarkBox
        // 
        _remarkBox.Location = new Point(20, 208);
        _remarkBox.Name = "_remarkBox";
        _remarkBox.Size = new Size(340, 40);
        _remarkBox.TabIndex = 5;
        // 
        // _saveBtn
        // 
        _saveBtn.Location = new Point(230, 260);
        _saveBtn.Name = "_saveBtn";
        _saveBtn.Size = new Size(70, 32);
        _saveBtn.TabIndex = 6;
        _saveBtn.Text = "保存";
        _saveBtn.Click += SaveBtn_Click;
        // 
        // _cancelBtn
        // 
        _cancelBtn.Location = new Point(306, 260);
        _cancelBtn.Name = "_cancelBtn";
        _cancelBtn.Size = new Size(60, 32);
        _cancelBtn.TabIndex = 7;
        _cancelBtn.Text = "取消";
        _cancelBtn.Click += CancelBtn_Click;
        // 
        // CustomerForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 320);
        Controls.Add(_cancelBtn);
        Controls.Add(_saveBtn);
        Controls.Add(_remarkBox);
        Controls.Add(_remarkLabel);
        Controls.Add(_contactBox);
        Controls.Add(_contactLabel);
        Controls.Add(_nameBox);
        Controls.Add(_nameLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "CustomerForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = _existing is null ? "新建客户" : "编辑客户";
        ResumeLayout(false);
        PerformLayout();
    }
}
