Imports System.Data
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Proxies.Internal
Imports MySqlConnector


Public Class ServerSync


    ''' <summary>
    ''' Raises an event when the server context was created in the background.
    ''' </summary>
    ''' 
    Public Shared Event ServerContextCreated(dbContext As ElnDataContext)


    ''' <summary>
    ''' Raises an event indicating the synchronization progress of the large tblEmbeddedFiles entries in percent.
    ''' </summary>
    ''' 
    Public Shared Event SyncProgress(syncPercent As Integer)


    ''' <summary>
    ''' Raises an event when the synchronization is complete.
    ''' </summary>
    ''' 
    Public Shared Event SyncComplete(sender As Object)


    Public Sub New(sqliteContext As ElnDbContext, mySQLContext As ElnDbContext)

        LocalContext = sqliteContext
        ServerContext = mySQLContext

    End Sub


    ''' <summary>
    ''' Gets if a synchronization currently is ongoing
    ''' </summary>
    ''' 
    Public Shared Property IsSynchronizing As Boolean = False


    ''' <summary>
    ''' Gets the GUID of the synchronizing local database
    ''' </summary>
    ''' 
    Public Shared Property DatabaseGUID As String


    ''' <summary>
    ''' Sets ro gets the local Sqlite database context
    ''' </summary>
    ''' 
    Private Property LocalContext As ElnDbContext


    ''' <summary>
    ''' Sets or gets the MySQL server database context
    ''' </summary>
    ''' 
    Public Shared Property ServerContext As ElnDbContext


    ''' <summary>
    ''' Gets if there are mismatching sync GUIDs which are preventing sync
    ''' </summary>
    '''
    Public Shared Property HasSyncMismatch As Boolean = False


    ''' <summary>
    ''' Sets or gets if the server connection was lost and a manual reconnect attempt is required.
    ''' </summary>
    ''' <remarks>To save CPU cycles, the application does not constantly try to reconnect after 
    ''' an ELN server connection loss. The user is presented a reconnect functionality instead, 
    ''' allowing to try to reconnect manually.</remarks>
    ''' 
    Public Shared Property IsServerConnectionLost As Boolean = False


    ''' <summary>
    ''' Gets the last server error after attempting to create the server context with CreateServerContextAsync. 
    ''' Returns an empty string if no error occurred.
    ''' </summary>
    ''' 
    Public Shared Property CreationErrorMessage As String = ""


    Friend Class ServerSyncItem

        Public Property KeyValue As String
        Public Property SyncState As Integer
        Public Property TableName As String
        Public Property ClonedEntity As Object
        Public Property SyncResult As SyncResult

    End Class


    ''' <summary>
    ''' Asynchronously creates the server context, and sets the specified max_packet_size (in MB), if the current one is smaller 
    ''' than the specified value.
    ''' </summary>
    ''' 
    Public Shared Async Sub CreateServerContextAsync(serverName As String, userName As String, password As String,
      port As Integer, localDbInfoEntry As tblDatabaseInfo)

        If Not IsSynchronizing Then

            Dim serverDbContext = Await Task.Run(Function() CreateMySQLContext(serverName, userName, password, port, localDbInfoEntry))
            RaiseEvent ServerContextCreated(serverDbContext)

        End If

    End Sub

    ''' <summary>
    ''' Executed during above async task
    ''' </summary>
    ''' 
    Public Shared Function CreateMySQLContext(serverName As String, userName As String, password As String, port As Integer,
      localDbInfoEntry As tblDatabaseInfo) As ElnDbContext

        Try

            Dim mySqlContext = New MySqlContext(serverName, userName, password, "PhoenixElnData", port)
            Dim serverDbContext = mySqlContext.ElnServerContext

            'test for required db upgrades
            If DbUpgradeServer.IsNewAppVersion Then
                DbUpgradeServer.Upgrade(serverDbContext.Database.GetConnectionString)
            End If

            'temporarily set required max_packet_size for connection
            Dim requiredPacketBytes = 60 * 1024 * 1024  '60 MB
            Try
                Dim dummy = serverDbContext.Database.SqlQueryRaw(Of Integer)("SET GLOBAL max_allowed_packet =" + requiredPacketBytes.ToString).AsEnumerable(0)
            Catch ex As Exception
                CreationErrorMessage = "The server user account requires the SUPER privilege to be set."
                Return Nothing
            End Try

            'check synchronization validity
            Dim serverDbInfo = (From info In serverDbContext.tblDatabaseInfo Where info.GUID = localDbInfoEntry.GUID).FirstOrDefault
            If serverDbInfo IsNot Nothing Then
                DatabaseGUID = serverDbInfo.GUID
                If serverDbInfo.LastSyncID <> localDbInfoEntry.LastSyncID Then
                    HasSyncMismatch = True
                End If
            Else
                '- db does not (yet) exist on server (e.g. before bulk upload) -> leave DatabaseGUID empty
            End If

            CreationErrorMessage = ""

            Return serverDbContext

            '- Debug: For testing success
            'Dim currMaxPacket = serverDbContext.Database.SqlQueryRaw(Of Integer)("SELECT @@global.max_allowed_packet").AsEnumerable(0)

        Catch ex As MySqlException

            Select Case ex.ErrorCode
                Case 1045
                    CreationErrorMessage = "Wrong username/password, please try again."
                Case 1042, 2013
                    CreationErrorMessage = "The ELN server is not available."
                Case 1862
                    CreationErrorMessage = "Your server password has expired."
                Case Else
                    CreationErrorMessage = ex.Message
            End Select

            Return Nothing

        End Try

    End Function


    ''' <summary>
    ''' Collects all sync info from the local database, then starts an async background process 
    ''' for propagating the changes to the server.
    ''' </summary>
    ''' <remarks>The synchronous part of this procedure is fast and strictly limited to the local data context, 
    ''' while the asynchronous (and much slower) part is strictly limited to the server data context.</remarks>
    ''' 
    Public Async Function SynchronizeAsync() As Task

        Try

            IsSynchronizing = True

            Dim syncItems As New List(Of ServerSyncItem)

            '- Get modified & added entry specs 

            Dim sqliteConnStr = LocalContext.Database.GetConnectionString
            Using sqliteConn = New SqliteConnection(sqliteConnStr)
                sqliteConn.Open()
                For Each tblInfo In ElnDbContext.TableInfo

                    Dim queryStr = "SELECT " + tblInfo.PrimaryKeyName + ", SyncState FROM " + tblInfo.TableName + " WHERE SyncState <> 0"
                    Using syncColCmd = New SqliteCommand(queryStr, sqliteConn)
                        Using colReader = syncColCmd.ExecuteReader
                            While colReader.Read
                                Dim syncItem As New ServerSyncItem
                                With syncItem
                                    .KeyValue = colReader.GetValue(0)
                                    .SyncState = colReader.GetValue(1)
                                    .TableName = tblInfo.TableName
                                    .SyncResult = SyncResult.Unprocessed
                                End With
                                syncItems.Add(syncItem)
                            End While
                        End Using
                    End Using

                Next
                sqliteConn.Close()
            End Using

            '- Get added and updated entries
            For Each syncItem In syncItems
                syncItem.ClonedEntity = CloneSyncEntity(syncItem)
            Next

            '- Get deleted entries
            For Each entry In LocalContext.sync_Tombstone
                Dim syncItem As New ServerSyncItem
                With syncItem
                    .KeyValue = entry.PrimaryKeyVal
                    .SyncState = 3   'means to delete
                    .TableName = entry.TableName
                    .ClonedEntity = Nothing
                    .SyncResult = SyncResult.Unprocessed
                End With
                syncItems.Add(syncItem)
            Next

            '- Start async server save process
            '----------------------------------
            '     Debug.WriteLine("server sync start: " + Now.ToString)
            Dim syncResults = Await SyncToServer(syncItems)
            '     Debug.WriteLine("server sync complete: " + Now.ToString)
            '-----------------------------------

            '- Server save completed: 
            If syncResults Is Nothing Then
                Exit Function  'lost server sync
            End If

            '- First time connected local db: assign dbGUID for later sync
            Dim dbInfo = LocalContext.tblDatabaseInfo.First
            If dbInfo.LastSyncID Is Nothing Then
                UpdateSyncInfo(dbInfo.GUID) 'also sets DatabaseGUID
            End If

            '- Reset sync info for successfully saved entries
            For Each syncItem In syncResults
                With syncItem
                    If syncItem.SyncResult = SyncResult.Completed Then
                        Select Case .SyncState
                            Case 1, 2
                                'reset sync state
                                Dim srcEntity = GetEntityByKey(LocalContext, .TableName, .KeyValue)
                                srcEntity.SyncState = 0
                            Case 3
                                'remove from tombstone
                                Dim tombstoneItem = (From entry In LocalContext.sync_Tombstone Where entry.PrimaryKeyVal = .KeyValue).FirstOrDefault
                                If tombstoneItem IsNot Nothing Then
                                    LocalContext.sync_Tombstone.Remove(tombstoneItem)
                                End If
                        End Select
                    Else
                        'all other entries were not saved and will be processed during the next sync.
                    End If
                End With
            Next

            LocalContext.SaveChangesNoSyncTracking()

            'assign new sync id's for this sync transaction
            UpdateSyncInfo(DatabaseGUID)

            RaiseEvent SyncComplete(Me)

        Catch ex As Exception

            'do nothing 

        Finally

            IsSynchronizing = False

        End Try

    End Function


    ''' <summary>
    ''' Synchronizes the changes specified by the syncItems list to the server, within a background process.
    ''' </summary>
    ''' <returns></returns>
    ''' 
    Private Async Function SyncToServer(syncItems As List(Of ServerSyncItem)) As Task(Of List(Of ServerSyncItem))

        Try

            RaiseEvent SyncProgress(0)

            '- first process everything except the large 'tblEmbeddedFiles' table

            For Each syncItem In syncItems
                With syncItem
                    If syncItem.TableName <> "tblEmbeddedFiles" Then
                        Select Case .SyncState
                            Case 1
                                UpdateServerEntry(syncItem)
                            Case 2
                                AddServerEntry(syncItem)
                            Case 3
                                RemoveServerEntry(syncItem)
                        End Select
                    End If
                End With
            Next

            '- Save changes to these small size records to server in the background
            Await ServerContext.SaveChangesNoSyncTrackingAsync()

            '- No server exception while writing (e.g. server lost): -> transition sync markers from PreProcessed to Completed 
            Dim res = From entry In syncItems Where entry.SyncResult = SyncResult.Preprocessed
            For Each item In res
                item.SyncResult = SyncResult.Completed
            Next

            '- Now sync the large embedded files entries individually to prevent exceeding
            '  the server max_allowed_packet limit

            Dim embeddedSyncItems = From entry In syncItems Where entry.TableName = "tblEmbeddedFiles"
            Dim itemsCount = embeddedSyncItems.Count
            Dim itemCount As Integer = 0

            For Each syncItem In embeddedSyncItems
                With syncItem

                    Select Case .SyncState
                        Case 1
                            UpdateServerEntry(syncItem)
                        Case 2
                            AddServerEntry(syncItem)
                        Case 3
                            RemoveServerEntry(syncItem)
                    End Select

                    'This is the time consuming background process due to the typically
                    'very large serialized document rows.
                    Await ServerContext.SaveChangesNoSyncTrackingAsync()

                    '- no server exception while writing (e.g. server lost): -> transition sync marker from PreProcessed to Completed
                    .SyncResult = SyncResult.Completed

                    itemCount += 1
                    RaiseEvent SyncProgress(CInt(100 * itemCount / itemsCount))

                End With

            Next

        Catch ex As Exception

            'do nothing

        End Try

        Return syncItems

    End Function


    ''' <summary>
    ''' Adds the entity specified by the provided syncEntry to the server.
    ''' </summary>
    ''' 
    Private Sub AddServerEntry(syncEntry As ServerSyncItem)

        With syncEntry

            Dim dstEntity = GetEntityByKey(ServerContext, .TableName, .KeyValue)
            If dstEntity Is Nothing Then
                ServerContext.Add(.ClonedEntity)
                .SyncResult = SyncResult.Preprocessed   'not yet written
            Else
                UpdateServerEntry(syncEntry)
            End If

        End With

    End Sub


    ''' <summary>
    ''' Updates the entity specified by the provided syncEntry to the server.
    ''' </summary>
    ''' 
    Private Sub UpdateServerEntry(syncEntry As ServerSyncItem)

        With syncEntry

            Dim dstEntity = GetEntityByKey(ServerContext, .TableName, .KeyValue)
            If dstEntity IsNot Nothing Then
                ServerContext.Entry(dstEntity).CurrentValues.SetValues(.ClonedEntity)
            Else
                AddServerEntry(syncEntry)
                .SyncResult = SyncResult.Preprocessed   'no yet written
            End If

        End With

    End Sub


    ''' <summary>
    ''' Removes the entity specified by the provided syncEntry from the server.
    ''' </summary>
    ''' 
    Private Sub RemoveServerEntry(syncEntry As ServerSyncItem)

        With syncEntry
            Dim dstEntity = GetEntityByKey(ServerContext, .TableName, .KeyValue)
            If dstEntity IsNot Nothing Then
                ServerContext.Remove(dstEntity)
                .SyncResult = SyncResult.Preprocessed  'not yet committed
            End If
        End With

    End Sub


    ''' <summary>
    ''' Gets a new entity created from the specified syncItem information- 
    ''' </summary>
    ''' 
    Private Function CloneSyncEntity(syncItem As ServerSyncItem) As Object

        With syncItem

            Dim srcEntity = GetEntityByKey(LocalContext, .TableName, .KeyValue)
            Dim srcValues = LocalContext.Entry(srcEntity).CurrentValues.Clone()

            Dim cpType = If(TypeOf srcEntity Is IProxyLazyLoader, srcEntity.GetType().BaseType, srcEntity.GetType())
            Dim serverEntity = Activator.CreateInstance(cpType) 'creates an empty new object
            LocalContext.Entry(serverEntity).CurrentValues.SetValues(srcValues)

            Return serverEntity

        End With

    End Function


    ''' <summary>
    ''' Gets the total size in MB of all embedded file entries marked for sync addition or update.
    ''' </summary>
    ''' 
    Public Function PendingEmbeddedTotalMB() As Integer

        Dim totalMB = LocalContext.Database.SqlQueryRaw(Of Integer)("SELECT SUM(FileSizeMB) FROM tblEmbeddedFiles WHERE SyncState > 0").AsEnumerable(0)
        Return totalMB

    End Function


    ''' <summary>
    ''' Resets all SyncStates across all tables to 0 and clears the sync_Tombstone table.
    ''' </summary>
    ''' <remarks>Typically utilized after bulk upload or for debug.</remarks>
    ''' 
    Public Sub ResetSyncFlags()

        '- Reset all entry syncStates
        For Each tblInfo In ElnDbContext.TableInfo
            Dim res = LocalContext.Database.SqlQueryRaw(Of Integer)("UPDATE " + tblInfo.TableName + " SET SyncState = 0 ").AsEnumerable(0)
        Next

        '- Clear sync_Tombstone entries
        LocalContext.sync_Tombstone.RemoveRange(LocalContext.sync_Tombstone)

        '- Save without sync change tracking
        LocalContext.SaveChangesNoSyncTracking()

    End Sub


    ''' <summary>
    ''' Assigns new, identical sync transaction parameters to local and server databases.
    ''' </summary>
    ''' <param name="localDbGuid">The GUID of the affected local database.</param>
    ''' 
    Public Sub UpdateSyncInfo(localDbGuid As String)

        Dim newSyncGuid = Guid.NewGuid.ToString("D")

        Dim serverInfo = (From info In ServerContext.tblDatabaseInfo Where info.GUID = localDbGuid).FirstOrDefault
        If serverInfo IsNot Nothing Then

            Dim syncTime = Now.ToString("yyyy-MM-dd HH:mm:ss")
            With LocalContext.tblDatabaseInfo.First
                .LastSyncID = newSyncGuid
                .LastSyncTime = syncTime
            End With
            serverInfo.LastSyncID = newSyncGuid
            serverInfo.LastSyncTime = syncTime

            DatabaseGUID = serverInfo.GUID

            LocalContext.SaveChangesNoSyncTracking()
            ServerContext.SaveChangesNoSyncTracking()
        End If
    End Sub


    ''' <summary>
    ''' Gets the entity specified by its name string and primary key value string
    ''' </summary>
    ''' 
    Public Shared Function GetEntityByKey(context As DbContext, tableName As String, keyValue As String) As Object

        Dim contextType = (From entry In context.Model.GetEntityTypes() Where entry.ClrType.Name = tableName
                           Select entry.ClrType).FirstOrDefault

        If contextType IsNot Nothing Then
            Return context.Find(contextType, keyValue)
        Else
            Return Nothing
        End If

    End Function


End Class
