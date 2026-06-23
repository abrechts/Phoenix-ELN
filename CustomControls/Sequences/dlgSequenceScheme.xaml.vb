
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnCoreModel


Partial Public Class dlgSequenceScheme

    Private _sequences As IReadOnlyList(Of SequenceGraph.SequenceNode)
    Private _queryExperiments As IQueryable(Of tblExperiments)
    Private _suppressZoomComboEvent As Boolean = False

    Public Sub New()
        InitializeComponent()
    End Sub


    ''' <summary>
    ''' Stores the data to render. Must be called before Show() so it is
    ''' available when Window_Loaded triggers the actual layout pass.
    ''' </summary>
    '''
    Public Sub SetData(sequences As IReadOnlyList(Of SequenceGraph.SequenceNode),
                       queryExperiments As IQueryable(Of tblExperiments))

        _sequences = sequences
        _queryExperiments = queryExperiments

    End Sub


    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)

        AddHandler SchemeView.ZoomChanged, AddressOf OnSchemeZoomChanged
        AddHandler SchemeView.FitAllAvailableChanged, AddressOf OnFitAllAvailableChanged

        ' BuildAndRender is deferred to here so SchemeCanvas is already part of
        ' the live visual tree — only then does UpdateLayout() produce valid
        ' DesiredSize values for the sequence panels (needed for column layout).
        If _sequences IsNot Nothing Then
            SchemeView.BuildAndRender(_sequences, _queryExperiments)
            Dim seqCount = _sequences.Count
            Dim stepCount = _sequences.Sum(Function(s) s.Members.Count)
            blkTitle.Text = $"STRUCTURE GRAPH  —  " &
                            $"{seqCount} Sequence{If(seqCount <> 1, "s", "")}, " &
                            $"{stepCount} Step{If(stepCount <> 1, "s", "")}"
        End If

    End Sub


    Private Sub btnPdfExport_PreviewMouseUp() Handles btnPdfExport.PreviewMouseUp

        Dim saveFileDlg As New Microsoft.Win32.SaveFileDialog() With {
            .Filter = "PDF file|*.pdf",
            .FileName = "Structure_Graph.pdf",
            .Title = "Export Scheme as PDF"
        }
        If Not saveFileDlg.ShowDialog() Then Return

        Me.Cursor = Cursors.Wait
        Me.ForceCursor = True
        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        SchemeView.ExportSchemeToPdf(saveFileDlg.FileName)

        Me.Cursor = Cursors.Arrow
        Me.ForceCursor = False

        Me.Topmost = True
        Me.Topmost = False
        Me.Activate()

    End Sub


    Private Sub ZoomCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

        If _suppressZoomComboEvent OrElse SchemeView Is Nothing Then Return

        Dim item = TryCast(ZoomCombo.SelectedItem, ComboBoxItem)
        If item Is Nothing Then Return

        Select Case CStr(item.Content)
            Case "100%" : SchemeView.ApplyZoom(1.0)
            Case "75%" : SchemeView.ApplyZoom(0.75)
            Case "50%" : SchemeView.ApplyZoom(0.5)
            Case "Fit All" : SchemeView.ApplyFitAllZoom()
        End Select

    End Sub


    Private Sub OnSchemeZoomChanged(zoom As Double)

        ZoomCombo.Tag = $"{CInt(Math.Round(zoom * 100))}%"
        _suppressZoomComboEvent = True
        Try
            Dim target As String = Nothing
            If Math.Abs(zoom - 1.0) < 0.001 Then
                target = "100%"
            ElseIf Math.Abs(zoom - 0.75) < 0.001 Then
                target = "75%"
            ElseIf Math.Abs(zoom - 0.5) < 0.001 Then
                target = "50%"
            End If
            ZoomCombo.SelectedItem = ZoomCombo.Items.OfType(Of ComboBoxItem)().
                FirstOrDefault(Function(i) CStr(i.Content) = target)
        Finally
            _suppressZoomComboEvent = False
        End Try

    End Sub


    Private Sub OnFitAllAvailableChanged(isAvailable As Boolean)

        FitAllItem.IsEnabled = isAvailable

    End Sub


    Private Sub Window_KeyDown(sender As Object, e As Input.KeyEventArgs)

        If e.Key = Input.Key.Escape Then Close()

    End Sub


    Private Sub btnClose_Click() Handles btnClose.Click

        Close()

    End Sub


End Class
