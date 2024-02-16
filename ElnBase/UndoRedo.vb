Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

''' <summary>
''' Tracks changes for a specific experiment and implements Undo/Redo functionality within this scope.
''' </summary>

Public Class UndoRedo

    Public Shared Event CanUndoChanged(sender As Object, isAvailable As Boolean)

    Public Shared Event CanRedoChanged(sender As Object, isAvailable As Boolean)


    Public Sub New(elnContext As ElnDataContext, ByRef expEntry As tblExperiments)

        TrackedExperiment = expEntry
        DbContext = elnContext

    End Sub


    ''' <summary>
    ''' Sets or gets the maximum allowed Undo steps.
    ''' </summary>
    ''' <remarks>Setting this value too high may result in memory issues!</remarks>
    ''' 
    Public Shared Property MaxUndoCount As Integer = 5

    ''' <summary>
    ''' Sets or gets the source experiment entry before any changes have occurred.  
    ''' </summary>
    ''' 
    Private Property TrackedExperiment As tblExperiments        '--> only works if latest changes are reflected here!

    ''' <summary>
    ''' Sets or gets the ELN database context
    ''' </summary>
    ''' 
    Private Property DbContext As ElnDbContext

    ''' <summary>
    ''' Gets the sequential list of Undo states. The last item is the most recent, the first one 
    ''' the oldest.
    ''' </summary>
    '''
    Private ReadOnly Property UndoHistory As New List(Of UndoEntry)

    ''' <summary>
    ''' Gets the sequential list of Redo operations. The last item is the most recent, the first one 
    ''' the oldest.
    ''' </summary>
    '''
    Private ReadOnly Property RedoHistory As New List(Of UndoEntry)


    ''' <summary>
    ''' Gets if any Undo operation is available.
    ''' </summary>
    ''' 
    Public Property CanUndo As Boolean
        Get
            Return _canUndo
        End Get
        Set(value As Boolean)
            If value <> _canUndo Then
                _canUndo = value
                RaiseEvent CanUndoChanged(Me, value)
            End If
        End Set
    End Property

    Private _canUndo As Boolean = False


    ''' <summary>
    ''' Gets if any Redo operation is available.
    ''' </summary>
    ''' 
    Public Property CanRedo As Boolean
        Get
            Return _canRedo
        End Get
        Set(value As Boolean)
            If value <> _canRedo Then
                _canRedo = value
                RaiseEvent CanRedoChanged(Me, value)
            End If
        End Set

    End Property

    Private _canRedo As Boolean = False


    ''' <summary>
    ''' Raises events for updating the current states of CanUndo and CanRedo. Typically applied after changing 
    ''' the data context of the experiment content.
    ''' </summary>
    ''' 
    Public Sub UpdateCanUndoRedo()

        RaiseEvent CanUndoChanged(Me, CanUndo)
        RaiseEvent CanRedoChanged(Me, CanRedo)

    End Sub


    ''' <summary>
    ''' Clears the Undo/Redo contents and notifies the UI that no more changes can occur.
    ''' </summary>
    ''' 
    Public Sub Reset()

        UndoHistory.Clear()
        RedoHistory.Clear()

        CanUndo = False
        CanRedo = False

        RaiseEvent CanUndoChanged(Me, False)
        RaiseEvent CanRedoChanged(Me, False)

    End Sub


    ''' <summary>
    ''' Appends a new undo entry to the Undo history, if any changes within the tracked experiment scope occurred.
    ''' </summary>
    ''' 
    Public Sub CreateUndoPoint(selectedItem As tblProtocolItems)

        Dim undoItem As New UndoEntry

        Dim trackerEntries = DbContext.ChangeTracker.Entries()
        If trackerEntries.Any Then

            With undoItem

                .SelectedProtocolItem = selectedItem

                '- modified
                Dim modifiedEntities = (From entry In trackerEntries Where entry.State = EntityState.Modified AndAlso
                    IsInTrackedExperimentScope(entry) Select entry.Entity).ToList
                For Each item In modifiedEntities
                    .ModifiedEntitiesUndo.Add(item, DbContext.Entry(item).OriginalValues.Clone)
                    .ModifiedEntitiesRedo.Add(item, DbContext.Entry(item).CurrentValues.Clone)
                Next

                '- added
                .AddedEntities = (From entry In trackerEntries Where entry.State = EntityState.Added AndAlso
                   IsInTrackedExperimentScope(entry) Select entry.Entity).ToList

                '- removed
                .RemovedEntities = (From entry In trackerEntries Where entry.State = EntityState.Deleted AndAlso
                   IsInTrackedExperimentScope(entry) Select entry.Entity).ToList

            End With

            If undoItem.ContainsChanges Then  'may contain no changes, if these were not in tblExperiments children scope

                '- add to Undo history
                With UndoHistory
                    If .Count >= MaxUndoCount Then
                        .Remove(UndoHistory.First)
                    End If
                    .Add(undoItem)
                End With

                '- clear Redo history (restarts Redo branch from new Undo point)
                RedoHistory.Clear()
                CanUndo = True
                CanRedo = False

            End If

        End If

    End Sub


    ''' <summary>
    ''' Performs an Undo operation
    ''' </summary>
    ''' 
    Public Function Undo() As tblProtocolItems

        If UndoHistory.Count > 0 Then

            Dim currUndo = UndoHistory.Last
            With currUndo

                For Each modified In .ModifiedEntitiesUndo
                    DbContext.Entry(modified.Key).CurrentValues.SetValues(modified.Value)
                    DbContext.Entry(modified.Key).State = EntityState.Modified
                Next

                For Each added In .AddedEntities
                    DbContext.Remove(added)
                Next

                For Each removed In .RemovedEntities
                    DbContext.Add(removed)
                    DbContext.RemoveTombstoneEntry(removed) 'removal needs to be explicit
                Next

            End With


            DbContext.SaveChanges()


            'remove from undo
            UndoHistory.Remove(UndoHistory.Last)

            'add to redo
            With RedoHistory
                If .Count >= MaxUndoCount Then
                    .Remove(RedoHistory.First)
                End If
                .Add(currUndo)
            End With

            CanUndo = UndoHistory.Any
            CanRedo = RedoHistory.Any

            Return currUndo.SelectedProtocolItem

        Else

            Return Nothing

        End If


    End Function


    ''' <summary>
    ''' Performs a Redo operation
    ''' </summary>
    ''' 
    Public Function Redo() As tblProtocolItems

        If RedoHistory.Count > 0 Then

            Dim currRedo = RedoHistory.Last
            With currRedo

                For Each modified In .ModifiedEntitiesRedo
                    DbContext.Entry(modified.Key).CurrentValues.SetValues(modified.Value)
                    DbContext.Entry(modified.Key).State = EntityState.Modified
                Next

                For Each added In .AddedEntities
                    DbContext.Add(added)
                    DbContext.RemoveTombstoneEntry(added)
                Next

                For Each removed In .RemovedEntities
                    DbContext.Remove(removed)
                Next

            End With

            DbContext.SaveChanges()

            'Remove from undo
            RedoHistory.Remove(RedoHistory.Last)
            With UndoHistory   'and add to undo
                If .Count >= MaxUndoCount Then
                    .Remove(UndoHistory.First)
                End If
                .Add(currRedo)
            End With

            CanUndo = UndoHistory.Any
            CanRedo = RedoHistory.Any

            Return currRedo.SelectedProtocolItem

        Else

            Return Nothing

        End If

    End Function


    ''' <summary>
    ''' Gets if the specified item entry is the TrackedExperiment ,or one of its downstream children.
    ''' </summary>
    ''' 
    Private Function IsInTrackedExperimentScope(itemEntry As ChangeTracking.EntityEntry) As Boolean

        'check for tblExperiments
        If TypeOf itemEntry.Entity Is tblExperiments Then
            If CType(itemEntry.Entity, tblExperiments).ExperimentID = TrackedExperiment.ExperimentID Then
                Return True
            End If
        End If

        'check for tblProtocolItems
        If TypeOf itemEntry.Entity Is tblProtocolItems Then
            If CType(itemEntry.Entity, tblProtocolItems).ExperimentID = TrackedExperiment.ExperimentID Then
                Return True
            End If
        End If

        'check for tblProtocolItems child
        Dim protocolItemRef = (From nav In itemEntry.Navigations Where nav.Metadata.Name = "ProtocolItem").FirstOrDefault
        If protocolItemRef IsNot Nothing Then
            If CType(protocolItemRef.EntityEntry.Entity.ProtocolItem, tblProtocolItems).ExperimentID = TrackedExperiment.ExperimentID Then
                Return True
            End If
        End If

        Return False

    End Function


    ''' <summary>
    ''' For DEBUG: Gets a string listing all modified properties of the specified *modified* entity. 
    ''' </summary>
    '''
    Private Function GetChangedEntityProperties(entity As Object) As String

        Dim modLine As String = ""

        modLine += entity.GetType.Name + vbCrLf + "-----------------------------------------" + vbCrLf
        Dim res = From prop In DbContext.Entry(entity).Properties Where prop.IsModified
        For Each modif In res
            modLine += modif.Metadata.Name + ": " + vbTab +
                   "curr: " + modif.CurrentValue.ToString + "; orig: " + modif.OriginalValue.ToString + vbCrLf
        Next
        modLine += vbCrLf

        Return modLine

    End Function



    Private Class UndoEntry

        Public Property SelectedProtocolItem As tblProtocolItems

        Public Property ModifiedEntitiesUndo As New Dictionary(Of Object, ChangeTracking.PropertyValues)

        Public Property ModifiedEntitiesRedo As New Dictionary(Of Object, ChangeTracking.PropertyValues)

        Public Property AddedEntities As List(Of Object)

        Public Property RemovedEntities As List(Of Object)

        Public ReadOnly Property ContainsChanges As Boolean
            Get
                Return ModifiedEntitiesUndo.Count > 0 OrElse AddedEntities.Count > 0 OrElse RemovedEntities.Count > 0
            End Get
        End Property

    End Class


End Class
