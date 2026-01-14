using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;

namespace PdfViewer.Desktop.Views;

public partial class DocumentPropertiesDialog : Window
{
    public DocumentPropertiesDialog()
    {
        InitializeComponent();
    }

    public void SetProperties(
        string filePath,
        string title,
        string author,
        string subject,
        int pageCount,
        double pageWidth,
        double pageHeight,
        DateTime? creationDate)
    {
        var fileNameText = this.FindControl<TextBlock>("FileNameText");
        var filePathText = this.FindControl<TextBlock>("FilePathText");
        var fileSizeText = this.FindControl<TextBlock>("FileSizeText");
        var titleText = this.FindControl<TextBlock>("TitleText");
        var authorText = this.FindControl<TextBlock>("AuthorText");
        var subjectText = this.FindControl<TextBlock>("SubjectText");
        var pageCountText = this.FindControl<TextBlock>("PageCountText");
        var pageSizeText = this.FindControl<TextBlock>("PageSizeText");
        var creationDateText = this.FindControl<TextBlock>("CreationDateText");

        if (fileNameText != null)
            fileNameText.Text = Path.GetFileName(filePath);

        if (filePathText != null)
            filePathText.Text = Path.GetDirectoryName(filePath) ?? "";

        if (fileSizeText != null)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                fileSizeText.Text = FormatFileSize(fileInfo.Length);
            }
            catch
            {
                fileSizeText.Text = "Unknown";
            }
        }

        if (titleText != null)
            titleText.Text = string.IsNullOrEmpty(title) ? "(Not specified)" : title;

        if (authorText != null)
            authorText.Text = string.IsNullOrEmpty(author) ? "(Not specified)" : author;

        if (subjectText != null)
            subjectText.Text = string.IsNullOrEmpty(subject) ? "(Not specified)" : subject;

        if (pageCountText != null)
            pageCountText.Text = pageCount.ToString();

        if (pageSizeText != null)
        {
            // Convert from points to inches (72 points = 1 inch)
            var widthInches = pageWidth / 72.0;
            var heightInches = pageHeight / 72.0;
            var widthMm = widthInches * 25.4;
            var heightMm = heightInches * 25.4;
            pageSizeText.Text = $"{widthMm:F0} x {heightMm:F0} mm ({widthInches:F1} x {heightInches:F1} in)";
        }

        if (creationDateText != null)
            creationDateText.Text = creationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(Not specified)";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
