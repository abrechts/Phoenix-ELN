
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

        'IMPORTANT: Foreign Keys=True is required with the winsqlite3 provider: Unlike e_sqlite3, the Windows
        'system SQLite is not compiled with SQLITE_DEFAULT_FOREIGN_KEYS, so ON DELETE CASCADE
        'would otherwise not be enforced!

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

        If TableInfo Is Nothing Then TableInfo = GetTablesInfo()

        'SearchIndex table: Don't rely solely on DbUpgradeLocal.Upgrade having already run against this particular database
        'file - its invocation is gated by the app's own version-change detection, which doesn't reliably
        'fire for every database this context could end up wrapping (e.g. after restore from server, etc)

        If Database.IsSqlite() Then
            FullTextSearch.EnsureSearchIndexTableExists(Me)
        End If

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

        'the full-text SearchIndex is an FTS5 virtual table specific to the local SQLite database - it doesn't
        'exist on the MySQL server context -> so save and exit here if in non-SqLite context

        If Not Database.IsSqlite() Then
            Return MyBase.SaveChanges()
        End If

        'capture the searchable changes now, while added/modified/deleted entity values and in-memory
        'relationship fixup (for same-unit-of-work parents) are still available

        Dim searchIndexOps = FullTextSearch.CollectSearchIndexOps(Me, added, modified, deleted)

        'both the entity changes and the SearchIndex maintenance must commit or roll back together,
        'since SearchIndex is a raw-SQL-maintained virtual table outside of EF's own change tracking/transaction

        Dim ownsTransaction = (Database.CurrentTransaction Is Nothing)
        Dim transaction = If(ownsTransaction, Database.BeginTransaction(), Database.CurrentTransaction)

        Try

            Dim result = MyBase.SaveChanges()

            FullTextSearch.ApplySearchIndexOps(Me, searchIndexOps)

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

