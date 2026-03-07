#nullable enable
namespace Atlas.LicenseIssuer.Forms;

partial class IssuanceLogForm
{
    private System.ComponentModel.IContainer? components = null;
    private DataGridView _grid = null!;
    private AntdUI.Button _refreshBtn = null!;

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
        _grid = new DataGridView();
        _refreshBtn = new AntdUI.Button();
        ((System.ComponentModel.ISupportInitialize)_grid).BeginInit();
        SuspendLayout();
        // 
        // _grid
        // 
        _grid.AllowUserToAddRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "操作类型", DataPropertyName = "Action", Width = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "客户ID", DataPropertyName = "CustomerId", Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "证书ID", DataPropertyName = "LicenseId", Width = 240 },
            new DataGridViewTextBoxColumn { HeaderText = "版本", DataPropertyName = "Revision", Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "套餐", DataPropertyName = "Edition", Width = 80 },
            new DataGridViewTextBoxColumn { HeaderText = "颁发时间", DataPropertyName = "IssuedAt", Width = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "到期时间", DataPropertyName = "ExpiresAt", Width = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark" }
        });
        _grid.Dock = DockStyle.Fill;
        _grid.Location = new Point(0, 0);
        _grid.Name = "_grid";
        _grid.ReadOnly = true;
        _grid.RowHeadersWidth = 62;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Size = new Size(900, 468);
        _grid.TabIndex = 0;
        // 
        // _refreshBtn
        // 
        _refreshBtn.Dock = DockStyle.Bottom;
        _refreshBtn.Location = new Point(0, 468);
        _refreshBtn.Name = "_refreshBtn";
        _refreshBtn.Size = new Size(900, 32);
        _refreshBtn.TabIndex = 1;
        _refreshBtn.Text = "刷新";
        _refreshBtn.Click += RefreshBtn_Click;
        // 
        // IssuanceLogForm
        // 
        AutoScaleDimensions = new SizeF(9F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 500);
        Controls.Add(_grid);
        Controls.Add(_refreshBtn);
        Name = "IssuanceLogForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "颁发日志";
        ((System.ComponentModel.ISupportInitialize)_grid).EndInit();
        ResumeLayout(false);
    }
}
