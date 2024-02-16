
Imports ElnCoreModel
Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore


Public Class SQLiteContext

    ''' <summary>
    ''' Create a new SQLite data context.
    ''' </summary>
    ''' <param name="sqliteFilePath">The file path to the SQLite database.</param>
    ''' 
    Public Sub New(sqliteFilePath As String)

        Dim optionsBuilder As New DbContextOptionsBuilder(Of ElnDataContext)
        Dim opts = optionsBuilder.UseSqlite("Data Source = " + sqliteFilePath)

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

        Return MyBase.SaveChanges

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

