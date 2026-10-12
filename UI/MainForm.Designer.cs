#nullable enable

using Microsoft.Web.WebView2.WinForms;

namespace CapaOptimizer.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components;
    private WebView2 webView = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        webView = new WebView2();
        ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
        SuspendLayout();
        webView.Dock = DockStyle.Fill;
        webView.Location = new Point(0, 0);
        webView.Name = "webView";
        webView.DefaultBackgroundColor = Color.FromArgb(10, 9, 20);
        webView.ZoomFactor = 1D;
        Controls.Add(webView);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(10, 9, 20);
        ClientSize = new Size(1120, 760);
        MinimumSize = new Size(760, 620);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Capa - Wi-Fi Anchor & Optimizer";
        FormBorderStyle = FormBorderStyle.Sizable;
        ((System.ComponentModel.ISupportInitialize)webView).EndInit();
        ResumeLayout(false);
    }
}
