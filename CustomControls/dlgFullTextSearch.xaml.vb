Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel

Public Class dlgFullTextSearch

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)

    Public Property LocalDBContext As ElnDbContext

    Private ReadOnly _fullTextSearch As New FullTextSearch


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        txtSearchTerm.Focus()

    End Sub


    Private Sub btnRunSearch_Click() Handles btnRunSearch.Click

        RunSearch()

    End Sub


    Private Sub txtSearchTerm_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtSearchTerm.PreviewKeyDown

        If e.Key = Key.Enter Then
            RunSearch()
        End If

    End Sub


    Private Sub RunSearch()

        If String.IsNullOrWhiteSpace(txtSearchTerm.Text) Then
            lstResults.ItemsSource = Nothing
            blkHitInfo.Text = ""
            Exit Sub
        End If

        Dim hits = _fullTextSearch.SearchExperiments(txtSearchTerm.Text, LocalDBContext).ToList()

        lstResults.ItemsSource = hits
        blkHitInfo.Text = $"{hits.Count} experiment(s) found"

    End Sub


    Private Sub lstResults_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstResults.PreviewMouseUp

        Dim selExp As tblExperiments = lstResults.SelectedItem

        If selExp IsNot Nothing Then
            Dim openArgs As New StepExpOpenArgs
            RaiseEvent RequestOpenExperiment(Me, selExp, False, openArgs)
        End If

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
        End If

    End Sub

End Class
