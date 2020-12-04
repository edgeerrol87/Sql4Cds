﻿namespace MarkMpn.Sql4Cds
{
    partial class SqlQueryControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SqlQueryControl));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.resultsTabPage = new System.Windows.Forms.TabPage();
            this.resultsFlowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.fetchXmlTabPage = new System.Windows.Forms.TabPage();
            this.fetchXMLFlowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.messagesTabPage = new System.Windows.Forms.TabPage();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.gridContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyWithHeadersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openRecordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createSELECTQueryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.hostLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.usernameLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.orgNameLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.timerLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.rowsLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.backgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.timer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.resultsTabPage.SuspendLayout();
            this.fetchXmlTabPage.SuspendLayout();
            this.gridContextMenuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.AutoScroll = true;
            this.splitContainer.Panel2.Controls.Add(this.tabControl);
            this.splitContainer.Panel2Collapsed = true;
            this.splitContainer.Size = new System.Drawing.Size(595, 452);
            this.splitContainer.SplitterDistance = 190;
            this.splitContainer.SplitterWidth = 2;
            this.splitContainer.TabIndex = 0;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.resultsTabPage);
            this.tabControl.Controls.Add(this.fetchXmlTabPage);
            this.tabControl.Controls.Add(this.messagesTabPage);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.ImageList = this.imageList;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(150, 46);
            this.tabControl.TabIndex = 0;
            // 
            // resultsTabPage
            // 
            this.resultsTabPage.Controls.Add(this.resultsFlowLayoutPanel);
            this.resultsTabPage.ImageIndex = 0;
            this.resultsTabPage.Location = new System.Drawing.Point(4, 23);
            this.resultsTabPage.Name = "resultsTabPage";
            this.resultsTabPage.Size = new System.Drawing.Size(142, 19);
            this.resultsTabPage.TabIndex = 0;
            this.resultsTabPage.Text = "Results";
            this.resultsTabPage.UseVisualStyleBackColor = true;
            // 
            // resultsFlowLayoutPanel
            // 
            this.resultsFlowLayoutPanel.AutoScroll = true;
            this.resultsFlowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultsFlowLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.resultsFlowLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.resultsFlowLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            this.resultsFlowLayoutPanel.Name = "resultsFlowLayoutPanel";
            this.resultsFlowLayoutPanel.Size = new System.Drawing.Size(142, 19);
            this.resultsFlowLayoutPanel.TabIndex = 2;
            this.resultsFlowLayoutPanel.WrapContents = false;
            this.resultsFlowLayoutPanel.ClientSizeChanged += new System.EventHandler(this.ResizeLayoutPanel);
            // 
            // fetchXmlTabPage
            // 
            this.fetchXmlTabPage.Controls.Add(this.fetchXMLFlowLayoutPanel);
            this.fetchXmlTabPage.ImageIndex = 1;
            this.fetchXmlTabPage.Location = new System.Drawing.Point(4, 23);
            this.fetchXmlTabPage.Name = "fetchXmlTabPage";
            this.fetchXmlTabPage.Size = new System.Drawing.Size(142, 19);
            this.fetchXmlTabPage.TabIndex = 2;
            this.fetchXmlTabPage.Text = "FetchXML";
            this.fetchXmlTabPage.UseVisualStyleBackColor = true;
            // 
            // fetchXMLFlowLayoutPanel
            // 
            this.fetchXMLFlowLayoutPanel.AutoScroll = true;
            this.fetchXMLFlowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fetchXMLFlowLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.fetchXMLFlowLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.fetchXMLFlowLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            this.fetchXMLFlowLayoutPanel.Name = "fetchXMLFlowLayoutPanel";
            this.fetchXMLFlowLayoutPanel.Size = new System.Drawing.Size(142, 19);
            this.fetchXMLFlowLayoutPanel.TabIndex = 1;
            this.fetchXMLFlowLayoutPanel.WrapContents = false;
            this.fetchXMLFlowLayoutPanel.ClientSizeChanged += new System.EventHandler(this.ResizeLayoutPanel);
            // 
            // messagesTabPage
            // 
            this.messagesTabPage.ImageIndex = 2;
            this.messagesTabPage.Location = new System.Drawing.Point(4, 23);
            this.messagesTabPage.Name = "messagesTabPage";
            this.messagesTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.messagesTabPage.Size = new System.Drawing.Size(142, 19);
            this.messagesTabPage.TabIndex = 1;
            this.messagesTabPage.Text = "Messages";
            this.messagesTabPage.UseVisualStyleBackColor = true;
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList.Images.SetKeyName(0, "Table_16x.png");
            this.imageList.Images.SetKeyName(1, "ExecutionPlan_16x.png");
            this.imageList.Images.SetKeyName(2, "ServerReport_16x.png");
            // 
            // gridContextMenuStrip
            // 
            this.gridContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem,
            this.copyWithHeadersToolStripMenuItem,
            this.openRecordToolStripMenuItem,
            this.createSELECTQueryToolStripMenuItem});
            this.gridContextMenuStrip.Name = "gridContextMenuStrip";
            this.gridContextMenuStrip.Size = new System.Drawing.Size(183, 92);
            this.gridContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.gridContextMenuStrip_Opening);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // copyWithHeadersToolStripMenuItem
            // 
            this.copyWithHeadersToolStripMenuItem.Name = "copyWithHeadersToolStripMenuItem";
            this.copyWithHeadersToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.copyWithHeadersToolStripMenuItem.Text = "Copy with Headers";
            this.copyWithHeadersToolStripMenuItem.Click += new System.EventHandler(this.copyWithHeadersToolStripMenuItem_Click);
            // 
            // openRecordToolStripMenuItem
            // 
            this.openRecordToolStripMenuItem.Name = "openRecordToolStripMenuItem";
            this.openRecordToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.openRecordToolStripMenuItem.Text = "Open Record";
            this.openRecordToolStripMenuItem.Click += new System.EventHandler(this.openRecordToolStripMenuItem_Click);
            // 
            // createSELECTQueryToolStripMenuItem
            // 
            this.createSELECTQueryToolStripMenuItem.Name = "createSELECTQueryToolStripMenuItem";
            this.createSELECTQueryToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.createSELECTQueryToolStripMenuItem.Text = "Create SELECT query";
            this.createSELECTQueryToolStripMenuItem.Click += new System.EventHandler(this.createSELECTQueryToolStripMenuItem_Click);
            // 
            // statusStrip
            // 
            this.statusStrip.BackColor = System.Drawing.Color.Khaki;
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel,
            this.hostLabel,
            this.usernameLabel,
            this.orgNameLabel,
            this.timerLabel,
            this.rowsLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 452);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(595, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Image = global::MarkMpn.Sql4Cds.Properties.Resources.ConnectFilled_grey_16x;
            this.toolStripStatusLabel.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(143, 17);
            this.toolStripStatusLabel.Spring = true;
            this.toolStripStatusLabel.Text = "Connected";
            this.toolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // hostLabel
            // 
            this.hostLabel.Image = global::MarkMpn.Sql4Cds.Properties.Resources.timeline_lock_on_16x;
            this.hostLabel.Name = "hostLabel";
            this.hostLabel.Size = new System.Drawing.Size(164, 17);
            this.hostLabel.Text = "orgxxx.crm.dynamics.com";
            // 
            // usernameLabel
            // 
            this.usernameLabel.Name = "usernameLabel";
            this.usernameLabel.Size = new System.Drawing.Size(140, 17);
            this.usernameLabel.Text = "username@contoso.com";
            // 
            // orgNameLabel
            // 
            this.orgNameLabel.Name = "orgNameLabel";
            this.orgNameLabel.Size = new System.Drawing.Size(43, 17);
            this.orgNameLabel.Text = "orgxxx";
            // 
            // timerLabel
            // 
            this.timerLabel.Name = "timerLabel";
            this.timerLabel.Size = new System.Drawing.Size(49, 17);
            this.timerLabel.Text = "00:00:00";
            // 
            // rowsLabel
            // 
            this.rowsLabel.Name = "rowsLabel";
            this.rowsLabel.Size = new System.Drawing.Size(41, 17);
            this.rowsLabel.Text = "0 rows";
            // 
            // backgroundWorker
            // 
            this.backgroundWorker.WorkerReportsProgress = true;
            this.backgroundWorker.WorkerSupportsCancellation = true;
            this.backgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker_DoWork);
            this.backgroundWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker_ProgressChanged);
            this.backgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker_RunWorkerCompleted);
            // 
            // timer
            // 
            this.timer.Interval = 1000;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // SqlQueryControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(595, 474);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.statusStrip);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "SqlQueryControl";
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.resultsTabPage.ResumeLayout(false);
            this.fetchXmlTabPage.ResumeLayout(false);
            this.gridContextMenuStrip.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ContextMenuStrip gridContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyWithHeadersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openRecordToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createSELECTQueryToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel hostLabel;
        private System.Windows.Forms.ToolStripStatusLabel usernameLabel;
        private System.Windows.Forms.ToolStripStatusLabel timerLabel;
        private System.Windows.Forms.ToolStripStatusLabel rowsLabel;
        private System.Windows.Forms.ToolStripStatusLabel orgNameLabel;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage resultsTabPage;
        private System.Windows.Forms.TabPage fetchXmlTabPage;
        private System.Windows.Forms.TabPage messagesTabPage;
        private System.Windows.Forms.ImageList imageList;
        private System.ComponentModel.BackgroundWorker backgroundWorker;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.FlowLayoutPanel fetchXMLFlowLayoutPanel;
        private System.Windows.Forms.FlowLayoutPanel resultsFlowLayoutPanel;
    }
}
