
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
                DbAddColumn("tblExperiments", "RxnIndigoObj", "LONGBLOB", "", "IsNodeExpanded", serverConn)
                DbAddColumn("tblExperiments", "RxnFingerprint", "BLOB", "", "RxnIndigoObj", serverConn)
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
                DbAddColumn("tblMaterials", "CurrDocIndex", "SMALLINT", "0", "IsValidated", serverConn)
            End If

            ' --> introduced in version 2.6.0

            If Not DbColumnExists("tblUsers", "IsCurrent", serverConn) Then
                DbAddColumn("tblUsers", "IsCurrent", "TINYINT", "0", "IsSpellCheckEnabled", serverConn)
                DbAddColumn("tblUsers", "SequenceNr", "SMALLINT", "0", "IsCurrent", serverConn)
                UpdateRev1ServerFieldTypes(serverConn)  'changes are introduced in version 2.6.0
            End If

        End Using

    End Sub


    ''' <summary>
    ''' This server DB revision updates a large number of sever table field types (introduced in version 2.6.0)
    ''' </summary>
    ''' <remarks>Changes initial varchar(x) types for text input to less restricive TEXT 
    ''' and replaces initial bigint fields by smallint.</remarks>
    ''' 
    Private Shared Sub UpdateRev1ServerFieldTypes(serverConn As MySqlConnection)

        'changes were introduced in version 2.6.0; no need to repeat checks for later versions

        If GetFieldType("tblEmbeddedFiles", "FileName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblEmbeddedFiles", "FileName", "TEXT")
        End If
        If GetFieldType("tblEmbeddedFiles", "FileType", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblEmbeddedFiles", "FileType", "SMALLINT")
        End If

        If GetFieldType("tblDbMaterialFiles", "FileName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblDbMaterialFiles", "FileName", "TEXT")
        End If

        ' Update material name & source text fields, plus SpecifiedUnitType

        If GetFieldType("tblAuxiliaries", "SpecifiedUnitType", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblAuxiliaries", "SpecifiedUnitType", "SMALLINT")
        End If
        If GetFieldType("tblAuxiliaries", "Name", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblAuxiliaries", "Name", "TEXT")
        End If
        If GetFieldType("tblAuxiliaries", "Source", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblAuxiliaries", "Source", "TEXT")
        End If

        If GetFieldType("tblReagents", "SpecifiedUnitType", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblReagents", "SpecifiedUnitType", "SMALLINT")
        End If
        If GetFieldType("tblReagents", "Name", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblReagents", "Name", "TEXT")
        End If
        If GetFieldType("tblReagents", "Source", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblReagents", "Source", "TEXT")
        End If

        If GetFieldType("tblRefReactants", "SpecifiedUnitType", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblRefReactants", "SpecifiedUnitType", "SMALLINT")
        End If
        If GetFieldType("tblRefReactants", "Name", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblRefReactants", "Name", "TEXT")
        End If
        If GetFieldType("tblRefReactants", "Source", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblRefReactants", "Source", "TEXT")
        End If

        If GetFieldType("tblSolvents", "SpecifiedUnitType", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblSolvents", "SpecifiedUnitType", "SMALLINT")
        End If
        If GetFieldType("tblSolvents", "Name", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblSolvents", "Name", "TEXT")
        End If
        If GetFieldType("tblSolvents", "Source", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblSolvents", "Source", "TEXT")
        End If

        '---------------------

        If GetFieldType("tblProducts", "Name", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblProducts", "Name", "TEXT")
        End If
        If GetFieldType("tblProducts", "ElementalFormula", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblProducts", "ElementalFormula", "TEXT")
        End If

        If GetFieldType("tblComments", "CommentFlowDoc", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblComments", "CommentFlowDoc", "TEXT")
        End If

        If GetFieldType("tblProtocolItems", "SequenceNr", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblProtocolItems", "SequenceNr", "SMALLINT")
        End If

        If GetFieldType("tblProjects", "Title", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblProjects", "Title", "TEXT")
        End If
        If GetFieldType("tblProjects", "SequenceNr", serverConn) <> "smallint" Then
            ChangeFieldType(serverConn, "tblProjects", "SequenceNr", "SMALLINT")
        End If

        If GetFieldType("tblMaterials", "MatName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblMaterials", "MatName", "TEXT")
        End If
        If GetFieldType("tblMaterials", "MatSource", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblMaterials", "MatSource", "TEXT")
        End If

        If GetFieldType("tblSeparators", "Title", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblSeparators", "Title", "TEXT")
        End If

        '-----------------------

        If GetFieldType("tblUsers", "FirstName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "FirstName", "TEXT")
        End If
        If GetFieldType("tblUsers", "LastName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "LastName", "TEXT")
        End If
        If GetFieldType("tblUsers", "CompanyName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "CompanyName", "TEXT")
        End If
        If GetFieldType("tblUsers", "DepartmentName", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "DepartmentName", "TEXT")
        End If
        If GetFieldType("tblUsers", "City", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "City", "TEXT")
        End If
        If GetFieldType("tblUsers", "PWHint", serverConn) <> "text" Then
            ChangeFieldType(serverConn, "tblUsers", "PWHint", "TEXT")
        End If

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


    ''' <summary>
    ''' Adds a column to the specified table according to the specified values. If the default value is empty, it is not added.
    ''' </summary>
    ''' 
    Friend Shared Sub DbAddColumn(tableName As String, fieldName As String, fieldType As String, defaultValue As String, afterField As String,
        serverConn As MySqlConnection)

        Try
            If Not DbColumnExists(tableName, fieldName, serverConn) Then

                Dim sqlCommand As String
                If defaultValue = "" Then
                    sqlCommand = $"ALTER TABLE {tableName} ADD {fieldName} {fieldType} AFTER {afterField}"
                Else
                    sqlCommand = $"ALTER TABLE {tableName} ADD {fieldName} {fieldType} Default {defaultValue} AFTER {afterField}"
                End If
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
                Dim sqlCommand = "ALTER TABLE " + tableName + " ADD " + fieldName + " " + fieldType + " Default " + defaultValue + " AFTER " + afterField
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


    Friend Shared Function ChangeFieldType(serverConn As MySqlConnection, tableName As String, colName As String,
       newType As String, Optional defaultValue As String = "") As Boolean

        Dim sqlCommand As String
        If defaultValue = "" Then
            sqlCommand = $"ALTER TABLE {tableName} MODIFY {colName} {newType}"
        Else
            sqlCommand = $"ALTER TABLE {tableName} MODIFY {colName} {newType} Default {defaultValue}"
        End If

        Try
            Using command As New MySqlCommand(sqlCommand, serverConn)
                serverConn.Open()
                command.ExecuteNonQuery()
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
