Imports System.Windows
Imports System.Windows.Controls
Imports ElnBase
Imports ElnCoreModel

Public Class dlgTags

    ''' <summary>
    ''' Display-only wrapper pairing a tag with whether it's assigned to the experiment this dialog
    ''' was opened for (Nothing if opened for pure tag-set management, e.g. from the Search menu).
    ''' </summary>
    '''
    Private Class TagAssignmentRow
        Public Property Tag As tblTags
        Public Property IsAssigned As Boolean
        Public Property ShowCheckbox As Boolean
    End Class


    ''' <summary>
    ''' Raised after a tag was added, renamed, deleted, or its assignment to the current experiment
    ''' toggled, so an owner window showing tag chips (e.g. StepSummary) can refresh live while this
    ''' dialog is still open, instead of only on close.
    ''' </summary>
    '''
    Public Event TagsChanged As EventHandler

    Private ReadOnly localContext As ElnDbContext
    Private ReadOnly databaseID As String
    Private ReadOnly currExperiment As tblExperiments
    Private EditingTag As tblTags


    ''' <summary>
    ''' Opens the tag management dialog. If currExperiment is supplied, each row gets a checkbox to
    ''' assign/unassign that tag to/from it; if omitted, the dialog is pure tag-set management (add,
    ''' rename, delete) with no assignment checkboxes.
    ''' </summary>
    '''
    Public Sub New(dbContext As ElnDbContext, Optional currExperiment As tblExperiments = Nothing)

        InitializeComponent()

        localContext = dbContext
        Me.currExperiment = currExperiment
        databaseID = localContext.tblDatabaseInfo.First.GUID

        RefreshTagList()

    End Sub


    Private Sub RefreshTagList()

        Dim selectedTagGUID = EditingTag?.GUID

        lstTags.ItemsSource = localContext.tblTags.
            Where(Function(t) t.DatabaseID = databaseID).
            ToList().
            OrderBy(Function(t) t.TagName, StringComparer.OrdinalIgnoreCase).
            Select(Function(t) New TagAssignmentRow With {
                .Tag = t,
                .ShowCheckbox = (currExperiment IsNot Nothing),
                .IsAssigned = (currExperiment IsNot Nothing AndAlso
                    currExperiment.tblExperimentTags.Any(Function(et) et.TagID = t.GUID))
            }).
            ToList()

        If selectedTagGUID IsNot Nothing Then
            lstTags.SelectedItem = CType(lstTags.ItemsSource, List(Of TagAssignmentRow)).
                FirstOrDefault(Function(r) r.Tag.GUID = selectedTagGUID)
        End If

    End Sub


    Private Sub lstTags_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

        Dim selectedRow = TryCast(lstTags.SelectedItem, TagAssignmentRow)
        EditingTag = selectedRow?.Tag

        If EditingTag IsNot Nothing Then
            txtTagName.Text = EditingTag.TagName
            btnAddTag.Content = "Rename"
        Else
            txtTagName.Text = ""
            btnAddTag.Content = "Add"
        End If

    End Sub


    Private Sub btnAddTag_Click(sender As Object, e As RoutedEventArgs)

        Dim newName = txtTagName.Text.Trim()
        If newName = "" Then Exit Sub

        Dim existingTags = localContext.tblTags.Where(Function(t) t.DatabaseID = databaseID).ToList()
        Dim isDuplicate = existingTags.
            Where(Function(t) EditingTag Is Nothing OrElse t.GUID <> EditingTag.GUID).
            Any(Function(t) t.TagName.Equals(newName, StringComparison.OrdinalIgnoreCase))

        If isDuplicate Then
            cbMsgBox.Display("A tag named '" & newName & "' already exists.", MsgBoxStyle.Exclamation, "Duplicate Tag")
            Exit Sub
        End If

        If EditingTag IsNot Nothing Then

            EditingTag.TagName = newName
            localContext.SaveChanges()

        Else

            Dim newTag As New tblTags
            With newTag
                .GUID = Guid.NewGuid.ToString("d")
                .DatabaseID = databaseID
                .TagName = newName
            End With
            localContext.tblTags.Add(newTag)
            localContext.SaveChanges()

        End If

        lstTags.SelectedItem = Nothing
        EditingTag = Nothing
        txtTagName.Text = ""
        RefreshTagList()
        RaiseEvent TagsChanged(Me, EventArgs.Empty)

    End Sub


    Private Sub btnDeleteTag_Click(sender As Object, e As RoutedEventArgs)

        Dim tagEntry = CType(CType(sender, Button).DataContext, TagAssignmentRow).Tag
        Dim usageCount = tagEntry.tblExperimentTags.Count

        If usageCount > 0 Then
            Dim msg = "This tag is assigned to " & usageCount & " experiment(s). Delete it anyway?" & vbCrLf &
                "It will be removed from all of them."
            If cbMsgBox.Display(msg, MsgBoxStyle.YesNo + MsgBoxStyle.Question, "Delete Tag") <> MsgBoxResult.Yes Then
                Exit Sub
            End If
        End If

        localContext.tblTags.Remove(tagEntry)
        localContext.SaveChanges()

        If EditingTag Is tagEntry Then
            EditingTag = Nothing
            txtTagName.Text = ""
            btnAddTag.Content = "Add"
        End If

        RefreshTagList()
        RaiseEvent TagsChanged(Me, EventArgs.Empty)

    End Sub


    ''' <summary>
    ''' Handles both a real user click AND the Checked event WPF raises when data-binding sets an
    ''' already-True IsChecked on a freshly generated CheckBox (e.g. after RefreshTagList rebuilds the
    ''' list) - the membership check below makes the latter a no-op instead of inserting a duplicate
    ''' tblExperimentTags row (which the unique index on ExperimentID+TagID would otherwise reject).
    ''' </summary>
    '''
    Private Sub chkAssignTag_Checked(sender As Object, e As RoutedEventArgs)

        If currExperiment Is Nothing Then Exit Sub

        Dim tag = CType(CType(sender, CheckBox).DataContext, TagAssignmentRow).Tag
        If currExperiment.tblExperimentTags.Any(Function(et) et.TagID = tag.GUID) Then Exit Sub

        Dim newLink As New tblExperimentTags
        With newLink
            .GUID = Guid.NewGuid.ToString("d")
            .ExperimentID = currExperiment.ExperimentID
            .TagID = tag.GUID
        End With

        currExperiment.tblExperimentTags.Add(newLink)
        localContext.SaveChanges()

        RefreshTagList()
        RaiseEvent TagsChanged(Me, EventArgs.Empty)

    End Sub


    Private Sub chkAssignTag_Unchecked(sender As Object, e As RoutedEventArgs)

        If currExperiment Is Nothing Then Exit Sub

        Dim tag = CType(CType(sender, CheckBox).DataContext, TagAssignmentRow).Tag
        Dim link = currExperiment.tblExperimentTags.FirstOrDefault(Function(et) et.TagID = tag.GUID)
        If link Is Nothing Then Exit Sub

        localContext.tblExperimentTags.Remove(link)
        localContext.SaveChanges()

        RefreshTagList()
        RaiseEvent TagsChanged(Me, EventArgs.Empty)

    End Sub


    Private Sub btnClose_Click(sender As Object, e As RoutedEventArgs)

        Me.Close()

    End Sub

End Class
