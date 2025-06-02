
Imports System.IO
Imports ElnCoreModel

Public Class RestoreFromServer

    Private Shared Property ServerContext As ElnDbContext
    Private Shared Property TmpLocalContext As ElnDbContext
    Private Shared Property tblFoldersDataPresent As Boolean


    ''' <summary>
    ''' Restores the local experiments database specified by its GUID from the server to a temporary database "ELNData.tmp" located 
    ''' in the same folder as the currently active experiments database.
    ''' </summary>
    ''' 
    Public Shared Function Restore(serverConnStr As String, localDbPath As String, serverDbInfoGUID As String) As Boolean

        Try

            'copy working Sqlite database to tmp file for failsafe restore operation 
            Dim tmpRestorePath = Path.GetDirectoryName(localDbPath) + "\ElnData.tmp"
            File.Copy(localDbPath, tmpRestorePath, True)

            TmpLocalContext = New SQLiteContext(tmpRestorePath).ElnContext

            Using TmpLocalContext

                'copy local db to temp file and clear all local data
                With TmpLocalContext
                    Dim localDbEntry = .tblDatabaseInfo.First
                    .tblDatabaseInfo.Remove(localDbEntry)
                    .SaveChanges()
                End With

                ServerContext = New MySqlContext(serverConnStr).ElnServerContext

                Using ServerContext

                    Dim serverDbInfoEntry = (From info In ServerContext.tblDatabaseInfo Where info.GUID = serverDbInfoGUID).FirstOrDefault
                    If serverDbInfoEntry IsNot Nothing Then

                        'for legacy data check, to determine if source the client database was upgraded to at least v.3.0.0 
                        tblFoldersDataPresent = IsTblProjFoldersDataPresent(serverDbInfoEntry)

                        'get and transfer root entity
                        AddEntity(serverDbInfoEntry)

                        'core: iterate server db hierarchy starting from root
                        ProcessChildren(serverDbInfoEntry)

                        'Replace all local DisplayIndex values by nothing, except for IsCurrent=1 (may be -2 artefacts from local server exp views).
                        'Pinned experiments are unpinned in the process, as an inevitable but tolerable side effect.
                        Dim res = From exp In TmpLocalContext.tblExperiments Where exp.DisplayIndex IsNot Nothing
                        For Each exp In res
                            exp.DisplayIndex = If(exp.IsCurrent = 1, 0, Nothing)
                        Next

                        TmpLocalContext.SaveChangesNoSyncTracking()

                    End If
                End Using

            End Using

            Return True

        Catch ex As Exception

            MsgBox(ex.Message, MsgBoxStyle.Information, "Restore Error")
            Return False

        End Try


    End Function


    Private Shared Sub ProcessChildren(parentEntity As Object)

        'iterates down to the level of tblProtocolItems
        Dim parentTableName = ServerContext.Entry(parentEntity).Metadata.ShortName

        For Each coll In ServerContext.Entry(parentEntity).Collections

            coll.Load()
            Dim childTableName = coll.Metadata.Name

            'prevent double tblExperiments references in the presence of multiple foreign keys
            Dim isBlockedRelation = (parentTableName = "tblUsers" AndAlso childTableName = "tblExperiments")
            If tblFoldersDataPresent Then
                'do this only for non-legacy data to prevent broken relations to tblExperiments 
                isBlockedRelation = isBlockedRelation OrElse (parentTableName = "tblProjects" AndAlso childTableName = "tblExperiments")
            End If

            If Not isBlockedRelation Then

                Dim children = coll.CurrentValue
                For Each child In children

                    AddEntity(child)

                    'handle child final downstream 1:1 relationship if present

                    Dim navItems = ServerContext.Entry(child).Navigations
                    Dim collItems = ServerContext.Entry(child).Collections

                    If Not collItems.Any AndAlso navItems.Count > 1 Then

                        For Each nav In navItems
                            nav.Load()
                            If nav.CurrentValue IsNot Nothing AndAlso nav.CurrentValue IsNot parentEntity Then

                                Dim tableName = ServerContext.Entry(nav.CurrentValue).Metadata.ShortName
                                If tableName = "tblEmbeddedFiles" Then
                                    'intermediate save before adding large entity
                                    TmpLocalContext.SaveChangesNoSyncTracking()
                                End If

                                AddEntity(nav.CurrentValue)
                                Exit For

                            End If
                        Next

                    End If

                    ProcessChildren(child)

                Next
            End If
        Next

    End Sub


    Private Shared Sub AddEntity(serverEntity As Object)

        With TmpLocalContext

            Try

                Dim cpType = serverEntity.GetType().BaseType
                Dim localEntity = Activator.CreateInstance(cpType) 'creates an empty new object
                .Entry(localEntity).CurrentValues.SetValues(serverEntity)
                .Add(localEntity)

            Catch ex As Exception
                'do nothing
            End Try

        End With

    End Sub


    ''' <summary>
    ''' Determine if the specified source client database was upgraded to at least v.3.0.0 
    ''' to contain tblProjFolders data. 
    ''' </summary>
    ''' 
    Private Shared Function IsTblProjFoldersDataPresent(serverDbInfoEntry As tblDatabaseInfo) As Boolean

        Return New Version(serverDbInfoEntry.CurrAppVersion) >= New Version("3.0")

    End Function


End Class
