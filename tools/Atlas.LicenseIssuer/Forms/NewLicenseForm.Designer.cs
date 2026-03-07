#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class NewLicenseForm
{
    private System.ComponentModel.IContainer? components = null;
    private FlowLayoutPanel _rootPanel = null!;
    private Label _editionLabel = null!;
    private ComboBox _editionCombo = null!;
    private Label _expiryTypeLabel = null!;
    private FlowLayoutPanel _expiryTypePanel = null!;
    private AntdUI.Radio _fixedRadio = null!;
    private AntdUI.Radio _permanentRadio = null!;
    private Label _expiryDateLabel = null!;
    private DateTimePicker _expiryPicker = null!;
    private Label _featureDivider = null!;
    private FlowLayoutPanel _featurePanel = null!;
    private AntdUI.Checkbox _chkLowCode = null!;
    private AntdUI.Checkbox _chkWorkflow = null!;
    private AntdUI.Checkbox _chkApproval = null!;
    private AntdUI.Checkbox _chkAlert = null!;
    private AntdUI.Checkbox _chkOffline = null!;
    private AntdUI.Checkbox _chkMultiTenant = null!;
    private Label _limitsDivider = null!;
    private FlowLayoutPanel _maxAppsRow = null!;
    private Label _maxAppsLabel = null!;
    private NumericUpDown _maxApps = null!;
    private FlowLayoutPanel _maxUsersRow = null!;
    private Label _maxUsersLabel = null!;
    private NumericUpDown _maxUsers = null!;
    private FlowLayoutPanel _maxTenantsRow = null!;
    private Label _maxTenantsLabel = null!;
    private NumericUpDown _maxTenants = null!;
    private Label _bindDivider = null!;
    private FlowLayoutPanel _bindPanel = null!;
    private AntdUI.Radio _noBindRadio = null!;
    private AntdUI.Radio _bindRadio = null!;
    private AntdUI.Input _fingerprintBox = null!;
    private Label _remarkLabel = null!;
    private AntdUI.Input _remarkBox = null!;
    private FlowLayoutPanel _buttonRow = null!;
    private AntdUI.Button _signBtn = null!;
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
        _rootPanel = new FlowLayoutPanel();
        _editionLabel = new Label();
        _editionCombo = new ComboBox();
        _expiryTypeLabel = new Label();
        _expiryTypePanel = new FlowLayoutPanel();
        _fixedRadio = new AntdUI.Radio();
        _permanentRadio = new AntdUI.Radio();
        _expiryDateLabel = new Label();
        _expiryPicker = new DateTimePicker();
        _featureDivider = new Label();
        _featurePanel = new FlowLayoutPanel();
        _chkLowCode = new AntdUI.Checkbox();
        _chkWorkflow = new AntdUI.Checkbox();
        _chkApproval = new AntdUI.Checkbox();
        _chkAlert = new AntdUI.Checkbox();
        _chkOffline = new AntdUI.Checkbox();
        _chkMultiTenant = new AntdUI.Checkbox();
        _limitsDivider = new Label();
        _maxAppsRow = new FlowLayoutPanel();
        _maxAppsLabel = new Label();
        _maxApps = new NumericUpDown();
        _maxUsersRow = new FlowLayoutPanel();
        _maxUsersLabel = new Label();
        _maxUsers = new NumericUpDown();
        _maxTenantsRow = new FlowLayoutPanel();
        _maxTenantsLabel = new Label();
        _maxTenants = new NumericUpDown();
        _bindDivider = new Label();
        _bindPanel = new FlowLayoutPanel();
        _noBindRadio = new AntdUI.Radio();
        _bindRadio = new AntdUI.Radio();
        _fingerprintBox = new AntdUI.Input();
        _remarkLabel = new Label();
        _remarkBox = new AntdUI.Input();
        _buttonRow = new FlowLayoutPanel();
        _signBtn = new AntdUI.Button();
        _cancelBtn = new AntdUI.Button();
        _rootPanel.SuspendLayout();
        _expiryTypePanel.SuspendLayout();
        _featurePanel.SuspendLayout();
        _maxAppsRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_maxApps).BeginInit();
        _maxUsersRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_maxUsers).BeginInit();
        _maxTenantsRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_maxTenants).BeginInit();
        _bindPanel.SuspendLayout();
        _buttonRow.SuspendLayout();
        SuspendLayout();
        // 
        // _rootPanel
        // 
        _rootPanel.AutoScroll = true;
        _rootPanel.Controls.Add(_editionLabel);
        _rootPanel.Controls.Add(_editionCombo);
        _rootPanel.Controls.Add(_expiryTypeLabel);
        _rootPanel.Controls.Add(_expiryTypePanel);
        _rootPanel.Controls.Add(_expiryDateLabel);
        _rootPanel.Controls.Add(_expiryPicker);
        _rootPanel.Controls.Add(_featureDivider);
        _rootPanel.Controls.Add(_featurePanel);
        _rootPanel.Controls.Add(_limitsDivider);
        _rootPanel.Controls.Add(_maxAppsRow);
        _rootPanel.Controls.Add(_maxUsersRow);
        _rootPanel.Controls.Add(_maxTenantsRow);
        _rootPanel.Controls.Add(_bindDivider);
        _rootPanel.Controls.Add(_bindPanel);
        _rootPanel.Controls.Add(_fingerprintBox);
        _rootPanel.Controls.Add(_remarkLabel);
        _rootPanel.Controls.Add(_remarkBox);
        _rootPanel.Controls.Add(_buttonRow);
        _rootPanel.Dock = DockStyle.Fill;
        _rootPanel.FlowDirection = FlowDirection.TopDown;
        _rootPanel.Location = new Point(0, 0);
        _rootPanel.Name = "_rootPanel";
        _rootPanel.Padding = new Padding(16);
        _rootPanel.Size = new Size(560, 600);
        _rootPanel.TabIndex = 0;
        _rootPanel.WrapContents = false;
        // 
        // _editionLabel
        // 
        _editionLabel.AutoSize = true;
        _editionLabel.Location = new Point(19, 16);
        _editionLabel.Name = "_editionLabel";
        _editionLabel.Size = new Size(82, 24);
        _editionLabel.TabIndex = 0;
        _editionLabel.Text = "套餐版本：";
        // 
        // _editionCombo
        // 
        _editionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _editionCombo.FormattingEnabled = true;
        _editionCombo.Items.AddRange(new object[] { "Trial", "Pro", "Enterprise" });
        _editionCombo.Location = new Point(19, 43);
        _editionCombo.Name = "_editionCombo";
        _editionCombo.Size = new Size(200, 32);
        _editionCombo.TabIndex = 1;
        // 
        // _expiryTypeLabel
        // 
        _expiryTypeLabel.AutoSize = true;
        _expiryTypeLabel.Location = new Point(19, 78);
        _expiryTypeLabel.Name = "_expiryTypeLabel";
        _expiryTypeLabel.Size = new Size(82, 24);
        _expiryTypeLabel.TabIndex = 2;
        _expiryTypeLabel.Text = "有效期类型：";
        // 
        // _expiryTypePanel
        // 
        _expiryTypePanel.AutoSize = true;
        _expiryTypePanel.Controls.Add(_fixedRadio);
        _expiryTypePanel.Controls.Add(_permanentRadio);
        _expiryTypePanel.Location = new Point(19, 105);
        _expiryTypePanel.Name = "_expiryTypePanel";
        _expiryTypePanel.Size = new Size(201, 36);
        _expiryTypePanel.TabIndex = 3;
        // 
        // _fixedRadio
        // 
        _fixedRadio.AutoSize = true;
        _fixedRadio.Checked = true;
        _fixedRadio.Location = new Point(3, 3);
        _fixedRadio.Name = "_fixedRadio";
        _fixedRadio.Size = new Size(102, 30);
        _fixedRadio.TabIndex = 0;
        _fixedRadio.TabStop = true;
        _fixedRadio.Text = "固定期限";
        // 
        // _permanentRadio
        // 
        _permanentRadio.AutoSize = true;
        _permanentRadio.Location = new Point(111, 3);
        _permanentRadio.Name = "_permanentRadio";
        _permanentRadio.Size = new Size(87, 30);
        _permanentRadio.TabIndex = 1;
        _permanentRadio.Text = "永久";
        // 
        // _expiryDateLabel
        // 
        _expiryDateLabel.AutoSize = true;
        _expiryDateLabel.Location = new Point(19, 144);
        _expiryDateLabel.Name = "_expiryDateLabel";
        _expiryDateLabel.Size = new Size(82, 24);
        _expiryDateLabel.TabIndex = 4;
        _expiryDateLabel.Text = "到期日期：";
        // 
        // _expiryPicker
        // 
        _expiryPicker.Location = new Point(19, 171);
        _expiryPicker.Name = "_expiryPicker";
        _expiryPicker.Size = new Size(260, 30);
        _expiryPicker.TabIndex = 5;
        _expiryPicker.Value = DateTime.Today.AddYears(1);
        // 
        // _featureDivider
        // 
        _featureDivider.AutoSize = true;
        _featureDivider.ForeColor = Color.Gray;
        _featureDivider.Location = new Point(19, 204);
        _featureDivider.Name = "_featureDivider";
        _featureDivider.Size = new Size(110, 24);
        _featureDivider.TabIndex = 6;
        _featureDivider.Text = "── 功能开关 ──";
        // 
        // _featurePanel
        // 
        _featurePanel.AutoSize = true;
        _featurePanel.Controls.Add(_chkLowCode);
        _featurePanel.Controls.Add(_chkWorkflow);
        _featurePanel.Controls.Add(_chkApproval);
        _featurePanel.Controls.Add(_chkAlert);
        _featurePanel.Controls.Add(_chkOffline);
        _featurePanel.Controls.Add(_chkMultiTenant);
        _featurePanel.Location = new Point(19, 231);
        _featurePanel.Name = "_featurePanel";
        _featurePanel.Size = new Size(504, 36);
        _featurePanel.TabIndex = 7;
        // 
        // _chkLowCode
        // 
        _chkLowCode.AutoSize = true;
        _chkLowCode.Checked = true;
        _chkLowCode.Location = new Point(3, 3);
        _chkLowCode.Name = "_chkLowCode";
        _chkLowCode.Size = new Size(87, 30);
        _chkLowCode.TabIndex = 0;
        _chkLowCode.Text = "低代码";
        // 
        // _chkWorkflow
        // 
        _chkWorkflow.AutoSize = true;
        _chkWorkflow.Location = new Point(96, 3);
        _chkWorkflow.Name = "_chkWorkflow";
        _chkWorkflow.Size = new Size(87, 30);
        _chkWorkflow.TabIndex = 1;
        _chkWorkflow.Text = "工作流";
        // 
        // _chkApproval
        // 
        _chkApproval.AutoSize = true;
        _chkApproval.Location = new Point(189, 3);
        _chkApproval.Name = "_chkApproval";
        _chkApproval.Size = new Size(87, 30);
        _chkApproval.TabIndex = 2;
        _chkApproval.Text = "审批流";
        // 
        // _chkAlert
        // 
        _chkAlert.AutoSize = true;
        _chkAlert.Location = new Point(282, 3);
        _chkAlert.Name = "_chkAlert";
        _chkAlert.Size = new Size(102, 30);
        _chkAlert.TabIndex = 3;
        _chkAlert.Text = "告警管理";
        // 
        // _chkOffline
        // 
        _chkOffline.AutoSize = true;
        _chkOffline.Location = new Point(390, 3);
        _chkOffline.Name = "_chkOffline";
        _chkOffline.Size = new Size(102, 30);
        _chkOffline.TabIndex = 4;
        _chkOffline.Text = "离线部署";
        // 
        // _chkMultiTenant
        // 
        _chkMultiTenant.AutoSize = true;
        _chkMultiTenant.Location = new Point(3, 39);
        _chkMultiTenant.Name = "_chkMultiTenant";
        _chkMultiTenant.Size = new Size(102, 30);
        _chkMultiTenant.TabIndex = 5;
        _chkMultiTenant.Text = "多租户";
        // 
        // _limitsDivider
        // 
        _limitsDivider.AutoSize = true;
        _limitsDivider.ForeColor = Color.Gray;
        _limitsDivider.Location = new Point(19, 270);
        _limitsDivider.Name = "_limitsDivider";
        _limitsDivider.Size = new Size(246, 24);
        _limitsDivider.TabIndex = 8;
        _limitsDivider.Text = "── 数量限制（0 或 -1 表示不限）──";
        // 
        // _maxAppsRow
        // 
        _maxAppsRow.AutoSize = true;
        _maxAppsRow.Controls.Add(_maxAppsLabel);
        _maxAppsRow.Controls.Add(_maxApps);
        _maxAppsRow.Location = new Point(19, 297);
        _maxAppsRow.Name = "_maxAppsRow";
        _maxAppsRow.Size = new Size(231, 40);
        _maxAppsRow.TabIndex = 9;
        // 
        // _maxAppsLabel
        // 
        _maxAppsLabel.Location = new Point(3, 0);
        _maxAppsLabel.Name = "_maxAppsLabel";
        _maxAppsLabel.Size = new Size(120, 30);
        _maxAppsLabel.TabIndex = 0;
        _maxAppsLabel.Text = "最大应用数：";
        _maxAppsLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _maxApps
        // 
        _maxApps.Location = new Point(129, 3);
        _maxApps.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
        _maxApps.Minimum = new decimal(new int[] { 1, 0, 0, int.MinValue });
        _maxApps.Name = "_maxApps";
        _maxApps.Size = new Size(99, 30);
        _maxApps.TabIndex = 1;
        _maxApps.Value = new decimal(new int[] { 3, 0, 0, 0 });
        // 
        // _maxUsersRow
        // 
        _maxUsersRow.AutoSize = true;
        _maxUsersRow.Controls.Add(_maxUsersLabel);
        _maxUsersRow.Controls.Add(_maxUsers);
        _maxUsersRow.Location = new Point(19, 343);
        _maxUsersRow.Name = "_maxUsersRow";
        _maxUsersRow.Size = new Size(231, 40);
        _maxUsersRow.TabIndex = 10;
        // 
        // _maxUsersLabel
        // 
        _maxUsersLabel.Location = new Point(3, 0);
        _maxUsersLabel.Name = "_maxUsersLabel";
        _maxUsersLabel.Size = new Size(120, 30);
        _maxUsersLabel.TabIndex = 0;
        _maxUsersLabel.Text = "最大用户数：";
        _maxUsersLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _maxUsers
        // 
        _maxUsers.Location = new Point(129, 3);
        _maxUsers.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
        _maxUsers.Minimum = new decimal(new int[] { 1, 0, 0, int.MinValue });
        _maxUsers.Name = "_maxUsers";
        _maxUsers.Size = new Size(99, 30);
        _maxUsers.TabIndex = 1;
        _maxUsers.Value = new decimal(new int[] { 10, 0, 0, 0 });
        // 
        // _maxTenantsRow
        // 
        _maxTenantsRow.AutoSize = true;
        _maxTenantsRow.Controls.Add(_maxTenantsLabel);
        _maxTenantsRow.Controls.Add(_maxTenants);
        _maxTenantsRow.Location = new Point(19, 389);
        _maxTenantsRow.Name = "_maxTenantsRow";
        _maxTenantsRow.Size = new Size(231, 40);
        _maxTenantsRow.TabIndex = 11;
        // 
        // _maxTenantsLabel
        // 
        _maxTenantsLabel.Location = new Point(3, 0);
        _maxTenantsLabel.Name = "_maxTenantsLabel";
        _maxTenantsLabel.Size = new Size(120, 30);
        _maxTenantsLabel.TabIndex = 0;
        _maxTenantsLabel.Text = "最大租户数：";
        _maxTenantsLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _maxTenants
        // 
        _maxTenants.Location = new Point(129, 3);
        _maxTenants.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
        _maxTenants.Minimum = new decimal(new int[] { 1, 0, 0, int.MinValue });
        _maxTenants.Name = "_maxTenants";
        _maxTenants.Size = new Size(99, 30);
        _maxTenants.TabIndex = 1;
        _maxTenants.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _bindDivider
        // 
        _bindDivider.AutoSize = true;
        _bindDivider.ForeColor = Color.Gray;
        _bindDivider.Location = new Point(19, 432);
        _bindDivider.Name = "_bindDivider";
        _bindDivider.Size = new Size(110, 24);
        _bindDivider.TabIndex = 12;
        _bindDivider.Text = "── 机器绑定 ──";
        // 
        // _bindPanel
        // 
        _bindPanel.AutoSize = true;
        _bindPanel.Controls.Add(_noBindRadio);
        _bindPanel.Controls.Add(_bindRadio);
        _bindPanel.Location = new Point(19, 459);
        _bindPanel.Name = "_bindPanel";
        _bindPanel.Size = new Size(318, 36);
        _bindPanel.TabIndex = 13;
        // 
        // _noBindRadio
        // 
        _noBindRadio.AutoSize = true;
        _noBindRadio.Checked = true;
        _noBindRadio.Location = new Point(3, 3);
        _noBindRadio.Name = "_noBindRadio";
        _noBindRadio.Size = new Size(87, 30);
        _noBindRadio.TabIndex = 0;
        _noBindRadio.TabStop = true;
        _noBindRadio.Text = "不绑定";
        // 
        // _bindRadio
        // 
        _bindRadio.AutoSize = true;
        _bindRadio.Location = new Point(96, 3);
        _bindRadio.Name = "_bindRadio";
        _bindRadio.Size = new Size(219, 30);
        _bindRadio.TabIndex = 1;
        _bindRadio.Text = "绑定指定机器码";
        // 
        // _fingerprintBox
        // 
        _fingerprintBox.Enabled = false;
        _fingerprintBox.Location = new Point(19, 501);
        _fingerprintBox.Multiline = true;
        _fingerprintBox.Name = "_fingerprintBox";
        _fingerprintBox.PlaceholderText = "粘贴来自平台「获取机器码」接口的机器码";
        _fingerprintBox.Size = new Size(500, 48);
        _fingerprintBox.TabIndex = 14;
        // 
        // _remarkLabel
        // 
        _remarkLabel.AutoSize = true;
        _remarkLabel.Location = new Point(19, 552);
        _remarkLabel.Name = "_remarkLabel";
        _remarkLabel.Size = new Size(162, 24);
        _remarkLabel.TabIndex = 15;
        _remarkLabel.Text = "颁发备注（内部可见）：";
        // 
        // _remarkBox
        // 
        _remarkBox.Location = new Point(19, 579);
        _remarkBox.Multiline = true;
        _remarkBox.Name = "_remarkBox";
        _remarkBox.Size = new Size(500, 48);
        _remarkBox.TabIndex = 16;
        // 
        // _buttonRow
        // 
        _buttonRow.AutoSize = true;
        _buttonRow.Controls.Add(_signBtn);
        _buttonRow.Controls.Add(_cancelBtn);
        _buttonRow.Location = new Point(19, 633);
        _buttonRow.Name = "_buttonRow";
        _buttonRow.Size = new Size(211, 40);
        _buttonRow.TabIndex = 17;
        // 
        // _signBtn
        // 
        _signBtn.Location = new Point(3, 3);
        _signBtn.Name = "_signBtn";
        _signBtn.Size = new Size(130, 34);
        _signBtn.TabIndex = 0;
        _signBtn.Text = "生成并导出证书";
        // 
        // _cancelBtn
        // 
        _cancelBtn.Location = new Point(139, 3);
        _cancelBtn.Name = "_cancelBtn";
        _cancelBtn.Size = new Size(69, 34);
        _cancelBtn.TabIndex = 1;
        _cancelBtn.Text = "取消";
        // 
        // NewLicenseForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(560, 600);
        Controls.Add(_rootPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "NewLicenseForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "新建证书";
        _rootPanel.ResumeLayout(false);
        _rootPanel.PerformLayout();
        _expiryTypePanel.ResumeLayout(false);
        _expiryTypePanel.PerformLayout();
        _featurePanel.ResumeLayout(false);
        _featurePanel.PerformLayout();
        _maxAppsRow.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_maxApps).EndInit();
        _maxUsersRow.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_maxUsers).EndInit();
        _maxTenantsRow.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_maxTenants).EndInit();
        _bindPanel.ResumeLayout(false);
        _bindPanel.PerformLayout();
        _buttonRow.ResumeLayout(false);
        ResumeLayout(false);
    }
}
