Imports System.IO
Imports System.Text
Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Proxies.Internal

Imports System
Imports System.Collections.Generic
Imports MySqlConnector



' --> Creates the server database from entity data -> works!
'------------------------------------------------
'serverDbContext.Database.EnsureDeleted() '--> deletes the database - but looks as if it deletes the *contents* only!
'serverDbContext.Database.EnsureCreated()


Public Class MySqlBulkUpload

    Public Shared Function UploadSqliteToMySQL(localContext As ElnDbContext, serverContext As ElnDbContext) As Boolean

        Dim serverConnStr = serverContext.Database.GetConnectionString
        Dim sqliteConnStr = localContext.Database.GetConnectionString

        'first upload smaller non-byte() tables
        If UploadAllExceptEmbedded(sqliteConnStr, serverConnStr) Then

            serverContext.SaveChangesNoSyncTracking()

            'then upload large tblEmbeddedFiles (30% faster than with above script method)
            Dim success = UploadEmbedded(localContext, serverContext)
            If success Then
                localContext.ServerSynchronization.ResetSyncFlags()
            End If

            Return success

        Else

            Return False

        End If

    End Function


    ''' <summary>
    ''' Upload tblEmbeddedFiles table.
    ''' </summary>
    ''' <remarks>30% better performance for large table rows than with the more flexible script method used for the other tables.</remarks>
    '''
    Private Shared Function UploadEmbedded(localContext As ElnDbContext, serverContext As ElnDbContext) As Boolean

        With serverContext
            Try
                .ChangeTracker.AutoDetectChangesEnabled = False
                For Each embeddedEntry In localContext.tblEmbeddedFiles

                    Dim cpType = If(TypeOf embeddedEntry Is IProxyLazyLoader, embeddedEntry.GetType().BaseType, embeddedEntry.GetType())
                    Dim serverEntity = Activator.CreateInstance(cpType) 'creates an empty new object
                    .Entry(serverEntity).CurrentValues.SetValues(embeddedEntry)
                    .Add(serverEntity)
                    .SaveChangesNoSyncTracking()    'always save for not exceeding max_allowed_packet
                Next

                .ChangeTracker.AutoDetectChangesEnabled = True

                Return True

            Catch ex As Exception

                MsgBox(ex.Message, MsgBoxStyle.Information, "Upload Error")
                Return False

            End Try
        End With

    End Function


    Private Shared CurrTransAction As MySqlTransaction


    ''' <summary>
    ''' Uploads the complete content of all SQLite data tables to the MySQL server, for creating an initial synchronization  
    ''' snapshot.
    ''' </summary>
    ''' <remarks>Raises the SyncComplete event when completed.</remarks>
    '''
    Private Shared Function UploadAllExceptEmbedded(sqliteConnStr As String, serverConnStr As String) As Boolean

        Try

            Using serverConn = New MySqlConnection(serverConnStr)
                Using sourceConn = New SqliteConnection(sqliteConnStr + "; foreign keys=False")

                    'Switch off foreign key checks
                    '----------------------------------
                    serverConn.Open()
                    Using cmd2 = New MySqlCommand("SET foreign_key_checks = 0;", serverConn)
                        cmd2.ExecuteNonQuery()
                    End Using

                    'Perform the actual upload
                    '-----------------------------
                    sourceConn.Open()
                    Dim tableNames = GetDataTableNames(sourceConn)
                    Dim counter = 0
                    Using uploadTrans = serverConn.BeginTransaction

                        CurrTransAction = uploadTrans

                        For Each table In tableNames

                            If table <> "tblEmbeddedFiles" Then

                                UploadAllTableData(table, sourceConn, serverConn)
                                counter += 1
                                '     RaiseEvent SnapshotUploadProgress(Me, (100 * counter / tableNames.Count))
                            End If

                        Next

                        uploadTrans.Commit()

                        sourceConn.Close()
                        serverConn.Close()

                    End Using
                End Using
            End Using


            Return True

        Catch ex As Exception

            Return False

        End Try

    End Function


    ''' <summary>
    ''' Transfers all rows from the specified SQLite table to the corresponding MySQL table. The accumulated INSERT commands 
    ''' are transferred in chunks, which are split whenever they exceed 4 MB size. This can also result in e.g. a 20 MB chunk, if 
    ''' a single blob entry is of that size. 
    ''' </summary>
    ''' <param name="tableName">Name of the table to upload.</param>
    ''' <remarks></remarks>
    ''' 
    Friend Shared Sub UploadAllTableData(tableName As String, sourceConn As SqliteConnection, serverConn As MySqlConnection)

        Dim scriptWriter As New StringBuilder
        Dim cmdIntro As String = ""
        Dim maxCmdSize As Integer = 4 * 1024 * 1024  '4 MB
        Dim firstRun As Boolean = True

        Dim primaryKeyColName = GetPrimaryKeyColName(tableName, sourceConn)
        Using cmdInserted As New SqliteCommand("SELECT * FROM " + tableName, sourceConn)

            Using insReader = cmdInserted.ExecuteReader

                If Not insReader.HasRows Then
                    'exit if empty table
                    Exit Sub
                End If

                'get column names
                Dim colNames = GetSqliteTableColNames(tableName, sourceConn)

                While insReader.Read

                    With scriptWriter
                        'first run: get all table column names
                        '-------------------------------------------
                        If firstRun Then
                            'IGNORE skips duplicate key entry inserts; also converts all other errors to warnings
                            .Append("INSERT IGNORE INTO " + tableName + " (")   'ignore is important for RxnFileIndex & RefRxnID

                            For Each colName In colNames
                                .Append(colName)
                                .Append(","c)
                            Next

                            .Remove(.Length - 1, 1)
                            .Append(") VALUES ")
                            cmdIntro = .ToString
                            firstRun = False
                        End If

                        'collect commands and upload
                        '----------------------------------
                        If .Length < maxCmdSize Then
                            .Append(CreateLineDataString(insReader, colNames))   'append data until limit exceeded
                        Else
                            UploadBlock(scriptWriter, serverConn)     'finalize line and upload commands to server
                            .Clear()
                            .Append(cmdIntro)
                            .Append(CreateLineDataString(insReader, colNames))    'append pending row
                        End If

                    End With
                End While

                'upload commands if block limit never reached, or last remaining block
                '--------------------------------------------------------------------------------
                If scriptWriter.Length > 0 Then
                    UploadBlock(scriptWriter, serverConn)
                End If
                insReader.Close()

            End Using
        End Using
    End Sub


    Private Shared Function CreateLineDataString(insReader As SqliteDataReader, colNames As List(Of String)) As String

        Dim cmdBuilder As New Text.StringBuilder()

        With cmdBuilder

            .Append("("c)
            For Each colName In colNames
                .Append(ConvertDbValueToString(insReader.Item(colName), True, preserveEscape:=True))
                .Append(","c)
            Next
            .Remove(.Length - 1, 1)
            .Append(")," + vbCrLf)

        End With

        Return cmdBuilder.ToString

    End Function


    '' <summary>
    ''' Converts a value to the appropriate script string format.
    ''' </summary>
    ''' <param name="cellVal">The value to convert.</param>
    ''' <param name="realAsDouble">True, if all real values are to be converted to double, otherwise to single; 
    ''' important for e-sig in restore from server. </param>
    ''' <param name="preserveEscape">True, if the escape character '\' in strings is to be doubled to be preserved. This is 
    ''' mandatory when transferring strings to MySQL/MariaDB, since the escape character will be stripped off during 
    ''' transfer. </param>
    ''' <returns>String representation of the value.</returns>
    ''' 
    Friend Shared Function ConvertDbValueToString(cellVal As Object, Optional realAsDouble As Boolean = True,
      Optional preserveEscape As Boolean = True) As String

        Dim cellWriter As New Text.StringBuilder
        Dim cellStr As String

        If TypeOf cellVal Is Byte() Then

            Dim strBuilder As New Text.StringBuilder("X'")
            Dim res = ByteToHexConvert(cellVal)
            strBuilder.Append(res)
            strBuilder.Append("'"c)
            cellStr = strBuilder.ToString

        ElseIf TypeOf cellVal Is String Then

            With cellWriter                 'processing time doubles if no string builder is utilized!
                .Append(cellVal)
                .Replace("'", "''")         'escape out '
                If preserveEscape Then
                    .Replace("\", "\\")     'mandatory for transfer to MySQL/MariaDB
                End If
                .Insert(0, "'")
                .Append("'"c)
                cellStr = .ToString
            End With

        ElseIf TypeOf cellVal Is Boolean Then
            cellStr = If(cellVal = True, "1", "0")

        ElseIf TypeOf cellVal Is DateTime Then
            cellStr = "'" + Format(cellVal, "yyyy-MM-dd HH:mm:ss") + "'"

        ElseIf IsDBNull(cellVal) OrElse IsNothing(cellVal) Then
            cellStr = "NULL"

        ElseIf (TypeOf cellVal Is Single OrElse TypeOf cellVal Is Double) Then
            ' R-param (roundtrip): Ensures conversion of *all* available digits to string 
            '(otherwise rounded to most compact format) - important to keep digital signature!
            If TypeOf (cellVal) Is Single AndAlso Not realAsDouble Then
                cellStr = CType(cellVal, Single).ToString("r")
            Else
                cellStr = CType(cellVal, Double).ToString("r")
            End If
            cellStr = cellStr.Replace(",", ".")  'for invariant decimal separator "."

        Else
            cellStr = cellVal.ToString

        End If

        Return cellStr

    End Function


    ''' <summary>
    ''' Converts a byte array to a hexadecimal string.
    ''' </summary>
    ''' <param name="bytes">Byte array to convert.</param>
    ''' <returns>Hex string of byte array.</returns>
    ''' 
    Private Shared Function ByteToHexConvert(bytes As Byte()) As String

        'Source:
        'http://stackoverflow.com/questions/311165/how-do-you-convert-byte-array-to-hexadecimal-string-and-vice-versa/14333437#14333437
        'Very fast conversion ....

        Dim c As Char() = New Char(bytes.Length * 2 - 1) {}
        Dim b As Integer
        For i As Integer = 0 To bytes.Length - 1
            b = bytes(i) >> 4
            c(i * 2) = ChrW(55 + b + (((b - 10) >> 31) And -7))
            b = bytes(i) And &HF
            c(i * 2 + 1) = ChrW(55 + b + (((b - 10) >> 31) And -7))
        Next
        Return New String(c)

    End Function


    Private Shared Function UploadBlock(cmdBuilder As StringBuilder, serverConn As MySqlConnection) As Boolean

        With cmdBuilder
            'finalize command block
            .Remove(.Length - 3, 3)
            .Append(";"c)
            Try
                'upload to server
                Using importCmd = New MySqlCommand(.ToString, serverConn)
                    importCmd.Transaction = CurrTransAction
                    importCmd.ExecuteNonQuery()
                End Using
            Catch ex As Exception
                Throw New Exception(ex.Message)
                Return False
            End Try
            Return True
        End With
    End Function


    ''' <summary>
    ''' Gets a list of all data tables. Assumes the connection to be open.
    ''' </summary>
    ''' <returns>List of table names.</returns>
    ''' 
    Friend Shared Function GetDataTableNames(sqliteConn As SqliteConnection) As List(Of String)

        Dim tblList As New List(Of String)
        Dim resReader As SqliteDataReader

        Using tblCmd As New SqliteCommand("SELECT name FROM sqlite_master WHERE type='table'", sqliteConn)
            resReader = tblCmd.ExecuteReader()
            While resReader.Read
                tblList.Add(resReader.GetString(0))
            End While
            resReader.Close()
        End Using

        Return tblList

    End Function


    ''' <summary>
    ''' Gets the name of the primary key of the specified table.
    ''' </summary>
    ''' <param name="tblName">Target table name.</param>
    ''' <param name="sqliteConn">Connection to SQLite database.</param>
    ''' <returns>Name of the primary key column, or empty string if none present.</returns>
    ''' 
    Friend Shared Function GetPrimaryKeyColName(tblName As String, sqliteConn As SqliteConnection) As String

        Using colCmd = New SqliteCommand("PRAGMA table_info(" + tblName + ")", sqliteConn)
            Using colReader = colCmd.ExecuteReader
                While colReader.Read
                    If colReader.GetValue(5) = 1 Then '1=primary key
                        Return colReader.GetString(1)
                    End If
                End While
            End Using
        End Using

        Return ""

    End Function


    ''' <summary>
    ''' Gets a list of all column names of the specified table.
    ''' </summary>
    ''' <param name="tblName">Name of the target table.</param>
    ''' <param name="sqliteConn">Sqlite database connection.</param>
    ''' <returns>List of column name strings.</returns>
    ''' 
    Public Shared Function GetSqliteTableColNames(tblName As String, sqliteConn As SqliteConnection) As List(Of String)

        Dim nameList As New List(Of String)

        Using colCmd = New SqliteCommand("PRAGMA table_info(" + tblName + ")", sqliteConn)
            Using colReader = colCmd.ExecuteReader
                While colReader.Read
                    Dim colName As String = colReader.GetValue(1)
                    nameList.Add(colName)
                End While
            End Using
        End Using

        Return nameList

    End Function



End Class
