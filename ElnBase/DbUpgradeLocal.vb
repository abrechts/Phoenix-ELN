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

            'introduced in version 0.9.4 (RSS queries)
            If Not DbColumnExists("tblExperiments", "RxnIndigoObj", sqliteConn) Then
                DbAddColumn("tblExperiments", "RxnIndigoObj", "longblob", "", sqliteConn)
                DbAddColumn("tblExperiments", "RxnFingerprint", "blob", "", sqliteConn)
            End If

            'example:
            '    'introduced in version 1.3.0
            '    If Not DbColumnExists("tblUsers", "Test", sqliteConn) Then

            '        DbAddColumn("tblUsers", "Test", "varchar(20)", "", sqliteConn)

            '        Dim tblStr =
            '            "CREATE TABLE IF NOT EXISTS tblDummy (
            '             ID Guid PRIMARY KEY Not NULL ,
            '                ProjectID Guid Not NULL ,
            '                DocBytes blob,
            '                IconImage blob,
            '                Title varchar(100),
            '                Comments varchar 
            '            ,
            '                FOREIGN KEY([ProjectID])
            '                    REFERENCES [tblProjects]([ID]) 
            '                    On UPDATE CASCADE ON DELETE CASCADE
            '            );"
            '        DbExecuteCmd(tblStr, sqliteConn)

            '    End If

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
