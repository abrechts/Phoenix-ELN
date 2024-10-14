Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore

Public Class DbUpgradeLocal

    ''' <summary>
    ''' Checks the local SQLite database for required structural upgrades, and applies them if required.
    ''' </summary>
    ''' <returns>True, if an upgrade occurred.</returns>
    ''' <remarks></remarks>
    ''' 
    Public Shared Function Upgrade(sqlitePath As String) As Boolean

        '---> examples

        Using sqliteConn = New SqliteConnection("DataSource = " + sqlitePath + "; foreign keys=FALSE")

            'apply changes sequentially from initial ones to most recent ones

            ' -- introduced in version 0.9.4 (RSS queries)

            If Not DbColumnExists("tblExperiments", "RxnIndigoObj", sqliteConn) Then
                DbAddColumn("tblExperiments", "RxnIndigoObj", "longblob", "", sqliteConn)
                DbAddColumn("tblExperiments", "RxnFingerprint", "blob", "", sqliteConn)
            End If

            ' -- introduced in version 2.3.0

            Dim tblStr =
               "CREATE TABLE IF NOT EXISTS tblDbMaterialFiles (
                GUID VARCHAR(36) PRIMARY KEY NOT NULL, 
                DbMaterialID VARCHAR(36) NOT NULL REFERENCES tblMaterials(GUID) ON DELETE CASCADE, 
                FileName VARCHAR NOT NULL, 
                FileBytes BLOB NOT NULL, 
                FileSizeMB REAL, 
                IconImage BLOB, 
                SyncState INTEGER DEFAULT 0);

                CREATE INDEX IF NOT EXISTS idx_DbMaterialID ON tblDbMaterialFiles(DbMaterialID);"

            DbExecuteCmd(tblStr, sqliteConn)

            'add so far missing indices
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_DatabaseID ON tblUsers(DatabaseID);", sqliteConn)
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_ProjUserID ON tblProjects(UserID);", sqliteConn)
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_UserID ON tblExperiments(UserID);", sqliteConn)
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_ProjectID ON tblExperiments(ProjectID);", sqliteConn)
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_MatName ON tblMaterials(MatName);", sqliteConn)
            DbExecuteCmd("CREATE INDEX IF NOT EXISTS idx_DatabaseInfoID ON sync_Tombstone(DatabaseInfoID);", sqliteConn)
            DbExecuteCmd("CREATE UNIQUE INDEX IF NOT EXISTS unq_tblSeparators ON tblSeparators(ProtocolItemID);", sqliteConn)

        End Using

        Upgrade = True

    End Function


    ''' <summary>
    ''' Determines if the specified local database field exists
    ''' </summary>
    ''' <param name="tableName">Name of the data table.</param>
    ''' <param name="colName">Name of the field.</param>
    ''' <param name="sqliteConn">Database connection string.</param>
    ''' <returns>True, if field exists.</returns>
    ''' <remarks></remarks>

    Public Shared Function DbColumnExists(tableName As String, colName As String, sqliteConn As SqliteConnection) As Boolean

        Dim res As Object

        Dim sqlCommand = "Select " + colName + " FROM " + tableName + " LIMIT 1"
        Using command As New SqliteCommand(sqlCommand, sqliteConn)
            sqliteConn.Open()
            Try
                res = command.ExecuteScalar()
                Return True
            Catch ex As Exception
                'this command will cause an exception when executed, if table and/or column don't exist
                Return False
            Finally
                sqliteConn.Close()
            End Try
        End Using

    End Function


    ''' <summary>
    ''' Adds a column to the specified data table of the database, if the filed doesn't exist already
    ''' </summary>
    ''' <param name="tableName">Name of the data table.</param>
    ''' <param name="fieldName">Name of the new field.</param>
    ''' <param name="fieldType">Type of the new field.</param>
    ''' <param name="sqliteConn">Connection to SQLite database.</param>
    ''' <param name="defaultVal">"The default value of the new field."</param>
    ''' 
    Public Shared Sub DbAddColumn(tableName As String, fieldName As String, fieldType As String,
        defaultVal As String, sqliteConn As SqliteConnection)

        Try
            If Not DbColumnExists(tableName, fieldName, sqliteConn) Then
                Dim sqlCommand = "ALTER TABLE " + tableName + " ADD " + fieldName + " " + fieldType
                If defaultVal <> "" Then
                    sqlCommand += " DEFAULT " + defaultVal
                End If
                Using command As New SqliteCommand(sqlCommand, sqliteConn)
                    sqliteConn.Open()
                    command.ExecuteNonQuery()
                    sqliteConn.Close()
                End Using
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        End Try

    End Sub


    Public Shared Sub DbExecuteCmd(cmdStr As String, sqliteConn As SqliteConnection)

        Using command As New SqliteCommand(cmdStr, sqliteConn)
            sqliteConn.Open()
            command.ExecuteNonQuery()
            sqliteConn.Close()
        End Using

    End Sub


End Class
