
Imports MySqlConnector


Public Class DbUpgradeServer

    ''' <summary>
    ''' Sets or gets if a check for DB schema upgrade is required.
    ''' </summary>
    ''' <value></value>
    ''' <remarks>Typically set if a client update is detected.</remarks>
    ''' 
    Public Shared Property IsNewAppVersion As Boolean = False

    Public Shared Sub Upgrade(serverConnStr As String)

        'apply changes sequentially from initial ones to most recent ones

        Using serverConn = New MySqlConnection(serverConnStr)

            ' --> introduced in version 0.9.4 (RSS queries)

            If Not DbColumnExists("tblExperiments", "RxnIndigoObj", serverConn) Then
                DbAddColumn("tblExperiments", "RxnIndigoObj", "LONGBLOB", "IsNodeExpanded", serverConn)
                DbAddColumn("tblExperiments", "RxnFingerprint", "BLOB", "RxnIndigoObj", serverConn)
            End If

            ' --> introduced in version 2.3.0

            Dim tblStr =
               "CREATE TABLE IF NOT EXISTS tblDbMaterialFiles (
                GUID VARCHAR(36) PRIMARY KEY NOT NULL, 
                DbMaterialID VARCHAR(36) NOT NULL REFERENCES tblMaterials(GUID) ON DELETE CASCADE, 
                FileName VARCHAR(50) NOT NULL, 
                FileBytes LONGBLOB NOT NULL, 
                FileSizeMB REAL, 
                IconImage BLOB, 
                SyncState INTEGER DEFAULT 0);"

            DbExecuteCmd(tblStr, serverConn)

            ' --> introduced in version 2.4.0 

            If Not DbColumnExists("tblMaterials", "CurrDocIndex", serverConn) Then
                DbAddColumn("tblMaterials", "CurrDocIndex", "SMALLINT", "IsValidated", serverConn)
            End If

            ' --> introduced in version 2.6.0

            If Not DbColumnExists("tblUsers", "IsCurrent", serverConn) Then
                DbAddColumn("tblUsers", "IsCurrent", "TINYINT", "IsSpellCheckEnabled", serverConn)
            End If

        End Using

    End Sub


    Friend Shared Function DbColumnExists(tableName As String, colName As String, serverConn As MySqlConnection) As Boolean

        Dim sqlCommand = "Select " + colName + " FROM " + tableName + " LIMIT 1"
        Try
            Using command As New MySqlCommand(sqlCommand, serverConn)
                serverConn.Open()
                Dim res = command.ExecuteScalar()
                serverConn.Close()
                Return True
            End Using
        Catch ex As Exception
            'this command will cause an exception when executed, if table and/or column don't exist
            serverConn.Close()
            Return False
        End Try

    End Function


    Friend Shared Sub DbAddColumn(tableName As String, fieldName As String, fieldType As String, afterField As String, serverConn As MySqlConnection)

        Try
            If Not DbColumnExists(tableName, fieldName, serverConn) Then
                Dim sqlCommand = "ALTER TABLE " + tableName + " ADD " + fieldName + " " + fieldType + " AFTER " + afterField
                Using command As New MySqlCommand(sqlCommand, serverConn)
                    serverConn.Open()
                    command.ExecuteNonQuery()
                    serverConn.Close()
                End Using
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        End Try

    End Sub

    Friend Shared Sub DbAddColumnWithDefault(tableName As String, fieldName As String, fieldType As String, defaultValue As String,
       afterField As String, serverConn As MySqlConnection)

        Try
            If Not DbColumnExists(tableName, fieldName, serverConn) Then
                Dim sqlCommand = "ALTER TABLE " + tableName + " ADD " + fieldName + " " + fieldType + " DEFAULT " + defaultValue + " AFTER " + afterField
                Using command As New MySqlCommand(sqlCommand, serverConn)
                    serverConn.Open()
                    command.ExecuteNonQuery()
                    serverConn.Close()
                End Using
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        End Try

    End Sub


    Friend Shared Sub DbExecuteCmd(cmdStr As String, serverConn As MySqlConnection)

        Try
            Using command As New MySqlCommand(cmdStr, serverConn)
                serverConn.Open()
                command.ExecuteNonQuery()
                serverConn.Close()
            End Using
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Exclamation)
        End Try

    End Sub

    Friend Shared Function GetFieldType(tableName As String, colName As String, serverConn As MySqlConnection) As String

        Dim sqlCommand = "Select COLUMN_TYPE From information_schema.COLUMNS Where TABLE_Name = '" + tableName + "' And COLUMN_NAME = '" + colName + "'"
        Try
Using command As New MySqlCommand(sqlCommand, serverConn)
serverConn.Open()
Dim res = command.ExecuteScalar()
serverConn.Close()
Return res
End Using
Catch ex As Exception
'this command will cause an exception when executed, if table and/or column don't exist
            serverConn.Close()
            Return ""
        End Try

    End Function


    Friend Shared Function ChangeFieldType(tableName As String, colName As String, newType As String, serverConn As MySqlConnection) As Boolean

        Dim sqlCommand = "ALTER TABLE " + tableName + " MODIFY " + colName + " " + newType
        Try
            Using command As New MySqlCommand(sqlCommand, serverConn)
                serverConn.Open()
                Dim res = command.ExecuteScalar()
                serverConn.Close()
                Return True
            End Using
        Catch ex As Exception
            'this command will cause an exception when executed, if table and/or column don't exist
            serverConn.Close()
            Return False
        End Try

    End Function


    ''' <summary>
    ''' CAUTION: This is for use in exceptional cases, as for a DDL script introducing a never used field. 
    ''' Never use this for a field already being part of an entity model or already used otherwise, since 
    ''' this would break backward compatibility.
    ''' </summary>
    ''' 
    Friend Shared Function DbRemoveColumn(tableName As String, colName As String, serverConn As MySqlConnection) As Boolean

        Dim sqlCommand = "ALTER TABLE " + tableName + " DROP " + colName
        Try
            Using command As New MySqlCommand(sqlCommand, serverConn)
                serverConn.Open()
                Dim res = command.ExecuteScalar()
                serverConn.Close()
                Return True
            End Using
        Catch ex As Exception
            'this command will cause an exception when executed, if table and/or column don't exist
            serverConn.Close()
            Return False
        End Try

    End Function


End Class
