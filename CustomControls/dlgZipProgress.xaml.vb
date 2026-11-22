
Public Class dlgZipProgress

    Public Sub New()
        ' This call is required by the designer.
        InitializeComponent()
    End Sub


    Public Property ExperimentEntries As List(Of ElnCoreModel.tblExperiments)

    Public Property ZipFilePath As String


    Private Sub Me_Loaded() Handles Me.Loaded

        AddHandler ExperimentPrint.PdfExportProgress, AddressOf ExperimentPrint_PdfExportProgress
        AddHandler ExperimentPrint.PDFExportCompleted, AddressOf ExperimentPrint_PdfExportCompleted

    End Sub


    Private Sub Me_Rendered() Handles Me.ContentRendered

        ProgressBar.Value = 0
        StatusLabel.Text = "Starting..."
        '    WPFToolbox.WaitForPriority(Threading.DispatcherPriority.Render)

        ExperimentPrint.ExperimentsToPdfZip(ExperimentEntries, ZipFilePath)

    End Sub


    Private Sub ExperimentPrint_PdfExportProgress(percentComplete As Integer, statusMessage As String)

        ProgressBar.Value = percentComplete
        StatusLabel.Text = statusMessage
        blkPercent.Text = percentComplete.ToString() & "%"

        '    WPFToolbox.WaitForPriority(Threading.DispatcherPriority.Render)

    End Sub


    Private Sub ExperimentPrint_PdfExportCompleted(success As Boolean)

        StatusLabel.Text = "Export Completed!"
        ProgressBar.Value = 100
        btnCancel.Content = "Close"

        'the export itself raises no dialogs, so that it stays usable off the UI thread; anything it
        'had to leave out is reported here, once, instead of interrupting the run
        Dim skipped = ExperimentPrint.SkippedExperiments
        If skipped.Count > 0 Then

            Dim reasonLines = skipped.GroupBy(Function(s) s.Reason).
                Select(Function(g) "• " + ExperimentPrint.RenderResultText(g.Key) + ": " +
                       String.Join(", ", g.Select(Function(s) s.ExperimentID)))

            cbMsgBox.Display("These experiments are missing from the archive." + vbCrLf + vbCrLf +
                   String.Join(vbCrLf, reasonLines), MsgBoxStyle.Exclamation, "PDF Export")

        End If

    End Sub


    Private Sub btnCancelClick() Handles btnCancel.Click

        ExperimentPrint.ExportCancelled = True

        Me.DialogResult = True
        Me.Close()

    End Sub





End Class
