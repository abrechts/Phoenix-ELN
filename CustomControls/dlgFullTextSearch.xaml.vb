Imports System.Windows
Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel

Public Class dlgFullTextSearch

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)

    Public Property LocalDBContext As ElnDbContext
    Public Property ServerDBContext As ElnDbContext

    Private ReadOnly _fullTextSearch As New FullTextSearch

    ''' <summary>
    ''' Whether the currently displayed results (lstResults.ItemsSource) came from ServerDBContext - snapshotted
    ''' from chkServerSearch.IsChecked at the time RunSearch last ran, rather than re-read live from the checkbox
    ''' when a result is clicked, so toggling the checkbox after a search but before opening a result can't
    ''' misattribute a still-displayed local/server hit to the wrong context.
    ''' </summary>
    '''
    Private _lastSearchWasServer As Boolean


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        chkServerSearch.IsEnabled = ServerDBContext IsNot Nothing
        chkServerSearch.IsChecked = False

        txtSearchTerm.Focus()

    End Sub


    Private Sub Me_Closing() Handles Me.Closing

        My.Settings.dlgFullTextPosition = New System.Drawing.Point(Left, Top)
        My.Settings.dlgFullTextSize = New System.Drawing.Size(ActualWidth, ActualHeight)

    End Sub


    Private Sub btnRunSearch_Click() Handles btnRunSearch.Click

        RunSearch()

    End Sub


    Private Sub txtSearchTerm_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtSearchTerm.PreviewKeyDown

        If e.Key = Key.Enter Then
            RunSearch()
        End If

    End Sub


    Private Sub chkServer_Check() Handles chkServerSearch.Checked, chkServerSearch.Unchecked

        RunSearch()

    End Sub


    Private Sub RunSearch()

        If String.IsNullOrWhiteSpace(txtSearchTerm.Text) Then
            lstResults.ItemsSource = Nothing
            blkHitInfo.Text = ""
            Exit Sub
        End If

        _lastSearchWasServer = chkServerSearch.IsChecked
        Dim searchContext = If(_lastSearchWasServer, ServerDBContext, LocalDBContext)

        Dim result = _fullTextSearch.SearchExperiments(txtSearchTerm.Text, searchContext)

        lstResults.ItemsSource = result.Hits
        blkHitInfo.Text = $"{result.Hits.Count} experiment(s) found" +
            If(result.WasTruncated, $" - showing the {FullTextSearch.MaxDisplayedResults} best matches only", "")

    End Sub


    Private Sub lstResults_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstResults.PreviewMouseUp

        Dim selHit As ExperimentSearchHit = lstResults.SelectedItem

        If selHit IsNot Nothing Then
            Dim openArgs As New StepExpOpenArgs
            RaiseEvent RequestOpenExperiment(Me, selHit.Experiment, _lastSearchWasServer, openArgs)
        End If

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
        End If

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/FullText-Search.html") With {
           .UseShellExecute = True}
        Process.Start(info)

    End Sub


End Class
