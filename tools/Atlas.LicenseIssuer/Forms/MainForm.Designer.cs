#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _toolsMenu = null!;
    private ToolStripMenuItem _menuKeyManagement = null!;
    private ToolStripMenuItem _menuIssuanceLog = null!;
    private ToolStripMenuItem _menuExit = null!;
    private SplitContainer _splitContainer = null!;
    private Panel _leftPanel = null!;
    private AntdUI.Input _searchBox = null!;
    private ListBox _customerList = null!;
    private AntdUI.Button _addCustomerBtn = null!;
    private Panel _rightPanel = null!;
    private AntdUI.Label _customerDetailLabel = null!;
    private DataGridView _licenseGrid = null!;
    private FlowLayoutPanel _btnPanel = null!;
    private AntdUI.Button _newLicenseBtn = null!;
    private AntdUI.Button _renewBtn = null!;
    private AntdUI.Button _exportBtn = null!;

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
        _menuStrip = new MenuStrip();
        _toolsMenu = new ToolStripMenuItem();
        _menuKeyManagement = new ToolStripMenuItem();
        _menuIssuanceLog = new ToolStripMenuItem();
        _menuExit = new ToolStripMenuItem();
        _splitContainer = new SplitContainer();
        _leftPanel = new Panel();
        _customerList = new ListBox();
        _searchBox = new AntdUI.Input();
        _addCustomerBtn = new AntdUI.Button();
        _rightPanel = new Panel();
        _licenseGrid = new DataGridView();
        _customerDetailLabel = new AntdUI.Label();
        _btnPanel = new FlowLayoutPanel();
        _newLicenseBtn = new AntdUI.Button();
        _renewBtn = new AntdUI.Button();
        _exportBtn = new AntdUI.Button();
        _menuStrip.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitContainer).BeginInit();
        _splitContainer.Panel1.SuspendLayout();
        _splitContainer.Panel2.SuspendLayout();
        _splitContainer.SuspendLayout();
        _leftPanel.SuspendLayout();
        _rightPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_licenseGrid).BeginInit();
        _btnPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _menuStrip
        // 
        _menuStrip.ImageScalingSize = new Size(24, 24);
        _menuStrip.Items.AddRange(new ToolStripItem[] { _toolsMenu });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1000, 32);
        _menuStrip.TabIndex = 0;
        _menuStrip.Text = "menuStrip1";
        // 
        // _toolsMenu
        // 
        _toolsMenu.DropDownItems.AddRange(new ToolStripItem[] { _menuKeyManagement, _menuIssuanceLog, new ToolStripSeparator(), _menuExit });
        _toolsMenu.Name = "_toolsMenu";
        _toolsMenu.Size = new Size(80, 28);
        _toolsMenu.Text = "工具(&T)";
        // 
        // _menuKeyManagement
        // 
        _menuKeyManagement.Name = "_menuKeyManagement";
        _menuKeyManagement.Size = new Size(173, 34);
        _menuKeyManagement.Text = "密钥管理";
        _menuKeyManagement.Click += ToolsMenuKeyManagement_Click;
        // 
        // _menuIssuanceLog
        // 
        _menuIssuanceLog.Name = "_menuIssuanceLog";
        _menuIssuanceLog.Size = new Size(173, 34);
        _menuIssuanceLog.Text = "颁发日志";
        _menuIssuanceLog.Click += ToolsMenuIssuanceLog_Click;
        // 
        // _menuExit
        // 
        _menuExit.Name = "_menuExit";
        _menuExit.Size = new Size(173, 34);
        _menuExit.Text = "退出";
        _menuExit.Click += ToolsMenuExit_Click;
        // 
        // _splitContainer
        // 
        _splitContainer.Dock = DockStyle.Fill;
        _splitContainer.Location = new Point(0, 32);
        _splitContainer.Name = "_splitContainer";
        // 
        // _splitContainer.Panel1
        // 
        _splitContainer.Panel1.Controls.Add(_leftPanel);
        _splitContainer.Panel1MinSize = 180;
        // 
        // _splitContainer.Panel2
        // 
        _splitContainer.Panel2.Controls.Add(_rightPanel);
        _splitContainer.Panel2MinSize = 400;
        _splitContainer.Size = new Size(1000, 588);
        _splitContainer.SplitterDistance = 240;
        _splitContainer.TabIndex = 1;
        // 
        // _leftPanel
        // 
        _leftPanel.Controls.Add(_customerList);
        _leftPanel.Controls.Add(_searchBox);
        _leftPanel.Controls.Add(_addCustomerBtn);
        _leftPanel.Dock = DockStyle.Fill;
        _leftPanel.Location = new Point(0, 0);
        _leftPanel.Name = "_leftPanel";
        _leftPanel.Size = new Size(240, 588);
        _leftPanel.TabIndex = 0;
        // 
        // _customerList
        // 
        _customerList.DisplayMember = "Name";
        _customerList.Dock = DockStyle.Fill;
        _customerList.FormattingEnabled = true;
        _customerList.ItemHeight = 24;
        _customerList.Location = new Point(0, 40);
        _customerList.Name = "_customerList";
        _customerList.Size = new Size(240, 516);
        _customerList.TabIndex = 1;
        // 
        // _searchBox
        // 
        _searchBox.Dock = DockStyle.Top;
        _searchBox.Location = new Point(0, 0);
        _searchBox.Name = "_searchBox";
        _searchBox.PlaceholderText = "搜索客户...";
        _searchBox.Size = new Size(240, 40);
        _searchBox.TabIndex = 0;
        // 
        // _addCustomerBtn
        // 
        _addCustomerBtn.Dock = DockStyle.Bottom;
        _addCustomerBtn.Location = new Point(0, 556);
        _addCustomerBtn.Name = "_addCustomerBtn";
        _addCustomerBtn.Size = new Size(240, 32);
        _addCustomerBtn.TabIndex = 2;
        _addCustomerBtn.Text = "+ 新建客户";
        _addCustomerBtn.Click += AddCustomerBtn_Click;
        // 
        // _rightPanel
        // 
        _rightPanel.Controls.Add(_licenseGrid);
        _rightPanel.Controls.Add(_customerDetailLabel);
        _rightPanel.Controls.Add(_btnPanel);
        _rightPanel.Dock = DockStyle.Fill;
        _rightPanel.Location = new Point(0, 0);
        _rightPanel.Name = "_rightPanel";
        _rightPanel.Size = new Size(756, 588);
        _rightPanel.TabIndex = 0;
        // 
        // _licenseGrid
        // 
        _licenseGrid.AllowUserToAddRows = false;
        _licenseGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _licenseGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _licenseGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "证书ID", DataPropertyName = "LicenseId", Width = 200 },
            new DataGridViewTextBoxColumn { HeaderText = "套餐", DataPropertyName = "Edition", Width = 80 },
            new DataGridViewTextBoxColumn { HeaderText = "有效期", DataPropertyName = "ExpiresAt", Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "操作", DataPropertyName = "Action", Width = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "颁发时间", DataPropertyName = "IssuedAt" }
        });
        _licenseGrid.Dock = DockStyle.Fill;
        _licenseGrid.Location = new Point(0, 64);
        _licenseGrid.Name = "_licenseGrid";
        _licenseGrid.ReadOnly = true;
        _licenseGrid.RowHeadersWidth = 62;
        _licenseGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _licenseGrid.Size = new Size(756, 484);
        _licenseGrid.TabIndex = 1;
        // 
        // _customerDetailLabel
        // 
        _customerDetailLabel.Dock = DockStyle.Top;
        _customerDetailLabel.Font = new Font("Microsoft YaHei UI", 10F);
        _customerDetailLabel.Location = new Point(0, 0);
        _customerDetailLabel.Name = "_customerDetailLabel";
        _customerDetailLabel.Padding = new Padding(8);
        _customerDetailLabel.Size = new Size(756, 64);
        _customerDetailLabel.TabIndex = 0;
        _customerDetailLabel.Text = "← 请先选择客户";
        // 
        // _btnPanel
        // 
        _btnPanel.Controls.Add(_newLicenseBtn);
        _btnPanel.Controls.Add(_renewBtn);
        _btnPanel.Controls.Add(_exportBtn);
        _btnPanel.Dock = DockStyle.Bottom;
        _btnPanel.Location = new Point(0, 548);
        _btnPanel.Name = "_btnPanel";
        _btnPanel.Padding = new Padding(4);
        _btnPanel.Size = new Size(756, 40);
        _btnPanel.TabIndex = 2;
        // 
        // _newLicenseBtn
        // 
        _newLicenseBtn.Location = new Point(7, 7);
        _newLicenseBtn.Name = "_newLicenseBtn";
        _newLicenseBtn.Size = new Size(110, 30);
        _newLicenseBtn.TabIndex = 0;
        _newLicenseBtn.Text = "颁发新证书";
        // 
        // _renewBtn
        // 
        _renewBtn.Location = new Point(123, 7);
        _renewBtn.Name = "_renewBtn";
        _renewBtn.Size = new Size(100, 30);
        _renewBtn.TabIndex = 1;
        _renewBtn.Text = "续签/升级";
        // 
        // _exportBtn
        // 
        _exportBtn.Location = new Point(229, 7);
        _exportBtn.Name = "_exportBtn";
        _exportBtn.Size = new Size(120, 30);
        _exportBtn.TabIndex = 2;
        _exportBtn.Text = "导出最新证书";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 620);
        Controls.Add(_splitContainer);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Atlas License Issuer";
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        _splitContainer.Panel1.ResumeLayout(false);
        _splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitContainer).EndInit();
        _splitContainer.ResumeLayout(false);
        _leftPanel.ResumeLayout(false);
        _rightPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_licenseGrid).EndInit();
        _btnPanel.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
