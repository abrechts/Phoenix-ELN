Imports MySqlConnector
Imports System.Security

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

            'introduced in version 0.9.4 (RSS queries)
            If Not DbColumnExists("tblExperiments", "RxnIndigoObj", serverConn) Then
                DbAddColumn("tblExperiments", "RxnIndigoObj", "LONGBLOB", "IsNodeExpanded", serverConn)
                DbAddColumn("tblExperiments", "RxnFingerprint", "BLOB", "RxnIndigoObj", serverConn)
            End If

            'example:
            'table introduced in e.g. version 7.5.0 (exists check already included in command)
            'Dim cmd = "CREATE TABLE IF NOT EXISTS tblDummy (
            '    ID varchar(36) PRIMARY KEY Not NULL,
            '    ProjectID varchar(36) Not NULL, 
            '    Title varchar(100),
            '    DocBytes LONGBLOB,
            '    IconImage blob,
            '    Comments text,
            '    FOREIGN KEY(ProjectID)
            '        REFERENCES tblProjects(GUID) 
            '        On UPDATE CASCADE ON DELETE CASCADE
            '    ) engine=innodb;"
            'DbExecuteCmd(cmd, serverConn)

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
