
Imports System.Windows
Imports System.Windows.Documents
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore


Public Class SQLiteContext

    ''' <summary>
    ''' Create a new SQLite data context.
    ''' </summary>
    ''' <param name="sqliteFilePath">The file path to the SQLite database.</param>
    ''' 
    Public Sub New(sqliteFilePath As String)

        Dim optionsBuilder As New DbContextOptionsBuilder(Of ElnDataContext)

        'Foreign Keys=True is required with the winsqlite3 provider: unlike e_sqlite3, the Windows
        'system SQLite is not compiled with SQLITE_DEFAULT_FOREIGN_KEYS, so ON DELETE CASCADE
        'would otherwise not be enforced.
        Dim opts = optionsBuilder.UseSqlite("Data Source = " + sqliteFilePath + ";Foreign Keys=True")

        'Important! Lazy loading required to obtain populated hierarchical navigation model data.
        'Install Microsoft.EntityFrameworkCore.Proxies NuGet package to use the option below:
        opts.UseLazyLoadingProxies

        ElnContext = New ElnDbContext(opts.Options)

    End Sub


    Public Property ElnContext As ElnDbContext


End Class


Public Class ElnDbContext

    Inherits ElnDataContext


    Public Sub New(options As DbContextOptions)
        MyBase.New(options)
        TableInfo = GetTablesInfo()
    End Sub


    ''' <summary>
    ''' Gets a list of all table names present in the data model, with the associated primary key names 
    ''' </summary>
    ''' 
    Public Shared Property TableInfo As List(Of TableInfoEntry)


    Public Property ServerSynchronization As ServerSync


    ''' <summary>
    ''' Resets all synchronization flags and clears the sync_Tombstone table. Typically used after initial bulk upload to the server,
    ''' or after restoring from the server, where the initial flags were copied during the initial bulk upload.
    ''' </summary>
    ''' 
    Public Sub ResetSyncFlags()

        '- Reset all entry syncStates
        For Each tblInfo In ElnDbContext.TableInfo
            Dim res = Database.SqlQueryRaw(Of Integer)("UPDATE " + tblInfo.TableName + " SET SyncState = 0 ").AsEnumerable(0)
        Next

        '- Clear sync_Tombstone entries
        sync_Tombstone.RemoveRange(sync_Tombstone)

        '- Save without sync change tracking
        SaveChangesNoSyncTracking()

    End Sub


    ''' <summary>
    ''' Saves the changes to the database and writes change tracking information for 
    ''' modified, added and deleted entities.
    ''' </summary>
    ''' 
    Public Overrides Function SaveChanges() As Integer

        'get changes for server sync

        Dim modified = From entry In ChangeTracker.Entries Where entry.State = EntityState.Modified Select entry.Entity
        Dim added = From entry In ChangeTracker.Entries Where entry.State = EntityState.Added Select entry.Entity
        Dim deleted = From entry In ChangeTracker.Entries Where entry.State = EntityState.Deleted Select entry.Entity

        For Each modifiedItem In modified

            If Not IsSyncInfrastructureEntry(modifiedItem) Then
                If modifiedItem.SyncState <> 2 Then
                    modifiedItem.SyncState = 1
                End If
                Entry(modifiedItem).State = EntityState.Modified  'required here for unknown reasons, otherwise SyncState not persisted.
            End If

        Next

        For Each addedItem In added

            If Not IsSyncInfrastructureEntry(addedItem) Then
                addedItem.SyncState = 2
            End If

        Next

        For Each deletedItem In deleted

            If Not IsSyncInfrastructureEntry(deletedItem) Then
                AddTombstoneEntry(deletedItem)
            End If

        Next

        'capture the searchable changes now, while added/modified/deleted entity values and in-memory
        'relationship fixup (for same-unit-of-work parents) are still available
        Dim searchIndexOps = CollectSearchIndexOps(added, modified, deleted)

        'both the entity changes and the SearchIndex maintenance must commit or roll back together,
        'since SearchIndex is a raw-SQL-maintained virtual table outside of EF's own change tracking/transaction
        Dim ownsTransaction = (Database.CurrentTransaction Is Nothing)
        Dim transaction = If(ownsTransaction, Database.BeginTransaction(), Database.CurrentTransaction)

        Try

            Dim result = MyBase.SaveChanges()

            ApplySearchIndexOps(searchIndexOps)

            If ownsTransaction Then
                transaction.Commit()
            End If

            Return result

        Catch

            If ownsTransaction Then
                transaction.Rollback()
            End If
            Throw

        Finally

            If ownsTransaction Then
                transaction.Dispose()
            End If

        End Try

    End Function


    ''' <summary>
    ''' Represents a pending change to the full-text SearchIndex, derived from a single added, modified or
    ''' deleted protocol item satellite entity (reagent, product, solvent, comment, etc.).
    ''' </summary>
    '''
    Private Class SearchIndexOp

        Public Property ProtocolItemID As String
        Public Property ExperimentID As String
        Public Property Content As String
        Public Property IsDelete As Boolean

    End Class


    ''' <summary>
    ''' Builds the list of SearchIndex changes required for the given added, modified and deleted entities of
    ''' the current unit of work. Only entities belonging to a protocol item satellite table are considered.
    ''' </summary>
    '''
    Private Function CollectSearchIndexOps(added As IEnumerable(Of Object), modified As IEnumerable(Of Object),
        deleted As IEnumerable(Of Object)) As List(Of SearchIndexOp)

        Dim ops As New List(Of SearchIndexOp)

        For Each entity In added.Concat(modified)

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                ops.Add(New SearchIndexOp With {
                    .ProtocolItemID = protocolItemID,
                    .ExperimentID = GetExperimentIDForProtocolItem(protocolItemID),
                    .Content = GetSearchableContent(entity),
                    .IsDelete = False
                })
            End If

        Next

        For Each entity In deleted

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                ops.Add(New SearchIndexOp With {.ProtocolItemID = protocolItemID, .IsDelete = True})
            End If

        Next

        Return ops

    End Function


    ''' <summary>
    ''' Applies the previously collected SearchIndex changes. Every change is a delete-then-(re)insert keyed by
    ''' ProtocolItemID, since FTS5 has no natural upsert and the table has no other unique constraint to rely on.
    ''' </summary>
    '''
    Private Sub ApplySearchIndexOps(ops As List(Of SearchIndexOp))

        For Each op In ops

            Database.ExecuteSqlRaw("DELETE FROM SearchIndex WHERE ProtocolItemID = {0}", op.ProtocolItemID)

            If Not op.IsDelete Then
                Database.ExecuteSqlRaw("INSERT INTO SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES ({0}, {1}, {2})",
                    op.ProtocolItemID, op.ExperimentID, op.Content)
            End If

        Next

    End Sub


    ''' <summary>
    ''' Gets if the specified entity belongs to one of the protocol item satellite tables that feed the
    ''' full-text SearchIndex, and if so, its owning ProtocolItemID.
    ''' </summary>
    '''
    Private Function TryGetSearchableProtocolItemID(entity As Object, ByRef protocolItemID As String) As Boolean

        Select Case True

            Case TypeOf entity Is tblReagents
                protocolItemID = DirectCast(entity, tblReagents).ProtocolItemID
            Case TypeOf entity Is tblProducts
                protocolItemID = DirectCast(entity, tblProducts).ProtocolItemID
            Case TypeOf entity Is tblSolvents
                protocolItemID = DirectCast(entity, tblSolvents).ProtocolItemID
            Case TypeOf entity Is tblAuxiliaries
                protocolItemID = DirectCast(entity, tblAuxiliaries).ProtocolItemID
            Case TypeOf entity Is tblRefReactants
                protocolItemID = DirectCast(entity, tblRefReactants).ProtocolItemID
            Case TypeOf entity Is tblSeparators
                protocolItemID = DirectCast(entity, tblSeparators).ProtocolItemID
            Case TypeOf entity Is tblEmbeddedFiles
                protocolItemID = DirectCast(entity, tblEmbeddedFiles).ProtocolItemID
            Case TypeOf entity Is tblComments
                protocolItemID = DirectCast(entity, tblComments).ProtocolItemID
            Case Else
                protocolItemID = Nothing
                Return False

        End Select

        Return True

    End Function


    ''' <summary>
    ''' Extracts the plain-text searchable content of a protocol item satellite entity.
    ''' </summary>
    '''
    Private Function GetSearchableContent(entity As Object) As String

        Select Case True

            Case TypeOf entity Is tblReagents
                Dim item = DirectCast(entity, tblReagents)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblProducts
                Return DirectCast(entity, tblProducts).Name
            Case TypeOf entity Is tblSolvents
                Dim item = DirectCast(entity, tblSolvents)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblAuxiliaries
                Dim item = DirectCast(entity, tblAuxiliaries)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblRefReactants
                Dim item = DirectCast(entity, tblRefReactants)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblSeparators
                Return DirectCast(entity, tblSeparators).Title
            Case TypeOf entity Is tblEmbeddedFiles
                Dim item = DirectCast(entity, tblEmbeddedFiles)
                Return item.FileName + " " + item.FileComment
            Case TypeOf entity Is tblComments
                Return ExtractPlainText(DirectCast(entity, tblComments).CommentFlowDoc)
            Case Else
                Return String.Empty

        End Select

    End Function


    ''' <summary>
    ''' Converts a comment's FlowDocument XAML into its plain-text content, for indexing purposes.
    ''' </summary>
    '''
    Private Function ExtractPlainText(flowDocXaml As String) As String

        If String.IsNullOrEmpty(flowDocXaml) Then
            Return String.Empty
        End If

        Try
            Dim doc = TryCast(Markup.XamlReader.Parse(flowDocXaml), FlowDocument)
            If doc Is Nothing Then
                Return String.Empty
            End If
            Return New TextRange(doc.ContentStart, doc.ContentEnd).Text
        Catch
            Return String.Empty
        End Try

    End Function


    ''' <summary>
    ''' Gets the owning ExperimentID for the given ProtocolItemID. Checks the change tracker first, since the
    ''' parent protocol item may have been added or modified within the same still-uncommitted unit of work.
    ''' </summary>
    '''
    Private Function GetExperimentIDForProtocolItem(protocolItemID As String) As String

        Dim trackedParent = ChangeTracker.Entries(Of tblProtocolItems)().
            FirstOrDefault(Function(e) e.Entity.GUID = protocolItemID AndAlso e.State <> EntityState.Detached)

        If trackedParent IsNot Nothing Then
            Return trackedParent.Entity.ExperimentID
        End If

        Return tblProtocolItems.AsNoTracking().
            Where(Function(pi) pi.GUID = protocolItemID).
            Select(Function(pi) pi.ExperimentID).
            FirstOrDefault()

    End Function


    ''' <summary>
    ''' Gets if the full-text SearchIndex currently contains no entries, e.g. because it was just created by
    ''' a schema upgrade and still needs its initial backfill via <see cref="RebuildSearchIndex"/>.
    ''' </summary>
    '''
    Public Function SearchIndexIsEmpty() As Boolean

        Return Database.SqlQueryRaw(Of Integer)("SELECT EXISTS(SELECT 1 FROM SearchIndex) AS Value").First() = 0

    End Function


    ''' <summary>
    ''' Rebuilds the full-text SearchIndex from scratch based on the current contents of all protocol item
    ''' satellite tables. Used for the initial backfill after the SearchIndex table is first created, and as a
    ''' manual repair option should the incremental index ever be suspected to have drifted.
    ''' </summary>
    '''
    Public Sub RebuildSearchIndex()

        Database.ExecuteSqlRaw("DELETE FROM SearchIndex")

        Dim experimentIDsByProtocolItem = tblProtocolItems.AsNoTracking().
            ToDictionary(Function(pi) pi.GUID, Function(pi) pi.ExperimentID)

        'materialize each satellite table individually first (.ToList()) - chaining .Concat() directly on the
        'IQueryable sources would make EF Core try to translate the whole union into a single incompatible SQL
        'set operation instead of combining them in memory.
        Dim allSatelliteEntities As IEnumerable(Of Object) =
            tblReagents.AsNoTracking().ToList().Cast(Of Object)().
            Concat(tblProducts.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblSolvents.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblAuxiliaries.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblRefReactants.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblSeparators.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblEmbeddedFiles.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(tblComments.AsNoTracking().ToList().Cast(Of Object)())

        For Each entity In allSatelliteEntities

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                Database.ExecuteSqlRaw("INSERT INTO SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES ({0}, {1}, {2})",
                    protocolItemID, experimentIDsByProtocolItem.GetValueOrDefault(protocolItemID), GetSearchableContent(entity))
            End If

        Next

    End Sub


    ''' <summary>
    ''' Saves the changes to the database without assigning change tracking information.
    ''' </summary>
    ''' <remarks>Typically used for saving to server context, or after resetting change tracking  
    ''' information after completed synchronization.</remarks>
    ''' 
    Public Function SaveChangesNoSyncTracking() As Integer

        Return MyBase.SaveChanges()

    End Function


    ''' <summary>
    ''' Asynchronously saves the changes to the database without assigning change tracking information.
    ''' </summary>
    ''' <remarks>Typically used for saving to server context, or after resetting change tracking  
    ''' information after completed synchronization.</remarks>
    ''' 
    Public Async Function SaveChangesNoSyncTrackingAsync() As Task(Of Integer)

        Return Await MyBase.SaveChangesAsync

    End Function


    ''' <summary>
    ''' Gets if the specified entity belongs to a synchronization infrastructure table. 
    ''' </summary>
    ''' 
    Private Function IsSyncInfrastructureEntry(entry As Object)

        Return (TypeOf entry Is sync_Tombstone)

    End Function


    Friend Sub RemoveTombstoneEntry(entity As Object)

        Dim itemKey = GetEntityKeyValue(entity)
        Dim tombstoneItem = (From entry In sync_Tombstone Where entry.PrimaryKeyVal = itemKey).FirstOrDefault

        If tombstoneItem IsNot Nothing Then
            sync_Tombstone.Remove(tombstoneItem)
        End If

    End Sub


    Friend Sub AddTombstoneEntry(entity As Object)

        Dim doomedEntry As New sync_Tombstone
        With doomedEntry
            .GUID = Guid.NewGuid.ToString("d")
            .DatabaseInfo = tblDatabaseInfo.First
            .TableName = Entry(entity).Metadata.GetTableName
            Dim keyName = ElnDbContext.TableInfo.Find(Function(x) x.TableName = .TableName).PrimaryKeyName
            .PrimaryKeyVal = Entry(entity).CurrentValues.Item(keyName)
        End With

        sync_Tombstone.Add(doomedEntry)

    End Sub


    ''' <summary>
    ''' Determines if the specified table exists in the context.
    ''' </summary>
    ''' 
    Public Function TableExists(tableName As String) As Boolean

        Dim res = From eType In Model.GetEntityTypes() Where eType.GetTableName.Equals(tableName.ToLower, StringComparison.OrdinalIgnoreCase)
        Return res.Any

    End Function


    ''' <summary>
    ''' Gets the table name and associated primary key name of all tables in the specified context.
    ''' </summary>
    ''' 
    Private Function GetTablesInfo() As List(Of TableInfoEntry)

        Dim infoList As New List(Of TableInfoEntry)

        Dim res = From eType In Model.GetEntityTypes() Select TableName = eType.GetTableName,
                PrimaryKeyName = eType.FindPrimaryKey.Properties(0).Name

        For Each item In res
            Dim info As New TableInfoEntry
            info.TableName = item.TableName
            info.PrimaryKeyName = item.PrimaryKeyName
            infoList.Add(info)
        Next

        Return infoList

    End Function


    Public Function GetEntityKeyValue(entity As Object) As String

        Dim tableName = Entry(entity).Metadata.GetTableName
        Dim keyName = (From entry In TableInfo Where entry.TableName = tableName Select entry.PrimaryKeyName).FirstOrDefault

        Return Entry(entity).CurrentValues.GetValue(Of String)(keyName)

    End Function


    Public Class TableInfoEntry

        Public Property TableName As String
        Public Property PrimaryKeyName As String

    End Class



End Class

