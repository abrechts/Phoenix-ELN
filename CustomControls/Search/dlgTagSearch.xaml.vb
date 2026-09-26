Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel

Public Class dlgTagSearch

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)

    ''' <summary>
    ''' Tags are a per-local-database, per-user custom set (see tblTags.DatabaseID) rather than a synced,
    ''' database-wide catalog, so this search only ever runs against the local database - unlike dlgSearch/
    ''' dlgFullTextSearch there is no server-context toggle.
    ''' </summary>
    '''
    Public Property LocalDBContext As ElnDbContext


    ''' <summary>
    ''' Display-only wrapper pairing a tag with whether it is currently selected as a filter criterion.
    ''' </summary>
    '''
    Private Class TagFilterRow
        Public Property Tag As tblTags
        Public Property IsSelected As Boolean
    End Class

    Private _tagRows As List(Of TagFilterRow)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        Dim databaseID = LocalDBContext.tblDatabaseInfo.First.GUID

        _tagRows = LocalDBContext.tblTags.
            Where(Function(t) t.DatabaseID = databaseID).
            ToList().
            Where(Function(t) t.tblExperimentTags.Count > 0).
            OrderBy(Function(t) t.TagName, StringComparer.OrdinalIgnoreCase).
            Select(Function(t) New TagFilterRow With {.Tag = t, .IsSelected = False}).
            ToList()

        icTags.ItemsSource = _tagRows

        UpdateResults()

    End Sub


    Private Sub Me_Closing() Handles Me.Closing

        My.Settings.dlgTagSearchPosition = New System.Drawing.Point(Left, Top)
        My.Settings.dlgTagSearchSize = New System.Drawing.Size(ActualWidth, ActualHeight)

    End Sub


    Private Sub tagToggle_Click(sender As Object, e As RoutedEventArgs)

        UpdateResults()

    End Sub


    ''' <summary>
    ''' Re-runs the tag filter and refreshes the results list. An experiment matches only if it carries
    ''' every currently selected tag (AND semantics, not "any of") - narrower each time another tag is
    ''' checked, matching how tag filters commonly work elsewhere (e.g. GitHub issue labels).
    ''' </summary>
    '''
    Private Sub UpdateResults()

        Dim selectedTagIDs = _tagRows.Where(Function(r) r.IsSelected).Select(Function(r) r.Tag.GUID).ToList()

        If selectedTagIDs.Count = 0 Then
            lstResults.ItemsSource = Nothing
            blkHitInfo.Text = ""
            blkPlaceholder.Text = "---  select tags to search  ---"
            Exit Sub
        End If

        Dim matchingExpIDs = LocalDBContext.tblExperimentTags.
            Where(Function(et) selectedTagIDs.Contains(et.TagID)).
            ToList().
            GroupBy(Function(et) et.ExperimentID).
            Where(Function(g) selectedTagIDs.All(Function(tagID) g.Any(Function(et) et.TagID = tagID))).
            Select(Function(g) g.Key).
            ToList()

        Dim sortedMatches = LocalDBContext.tblExperiments.
            Where(Function(exp) matchingExpIDs.Contains(exp.ExperimentID)).
            ToList().
            OrderByDescending(Function(exp) exp.ExperimentID).
            ToList()

        'cap displayed hits like SearchBase.MaxDisplayedResults - a widely-used tag could otherwise
        'flood the results list, same rationale as material/full-text search
        Dim wasTruncated = sortedMatches.Count > SearchBase.MaxDisplayedResults
        Dim matches = If(wasTruncated, sortedMatches.Take(SearchBase.MaxDisplayedResults).ToList(), sortedMatches)

        lstResults.ItemsSource = matches
        blkHitInfo.Text = SearchBase.BuildHitCountText(matches.Count, wasTruncated)
        blkPlaceholder.Text = "---  no matching experiments  ---"

    End Sub


    Private Sub lstResults_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstResults.PreviewMouseUp

        Dim selExp = TryCast(lstResults.SelectedItem, tblExperiments)

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
