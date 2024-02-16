
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore.Proxies.Internal

''' <summary>
''' When attempting to connect a non-demo database to the ELN server for the first time, 
''' it may turn out that the current user-ID by coincidence already is present on the server 
''' and therefore would block connection.
''' 
''' This class provides functionality for assigning a different userID across all affected 
''' database tables, to allow subsequent server connection and synchronization. Since this 
''' modifies existing experiment ID's, this is a last resort measure which only should be 
''' applied if the experiments are not yet published on the server, i.e. before 
''' first time connect!
''' 
''' Re-assigning is achieved by adding a new userID entry and cloning all affected tables to this new 
''' entry. If successful, the original username entries then are deleted.
''' 
''' </summary>
''' 
Public Class RenameUserID

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="localContext">The local ElbDbContext</param>
    ''' <param name="origUserID">The original userID to be replaced by a unique one</param>
    ''' <param name="newUserID">The new userID, which needs to be checked for server duplicates before.</param>
    ''' <returns></returns>
    '''
    Public Shared Function DoRename(localContext As ElnDbContext, origUserID As String, newUserID As String) As Boolean

        If origUserID.ToLower = newUserID.ToLower Then
            Return False
        End If

        Dim dbInfoEntry = localContext.tblDatabaseInfo.First

        Dim origUserEntry = (From user In dbInfoEntry.tblUsers Where user.UserID = origUserID).FirstOrDefault
        If origUserEntry Is Nothing Then
            Return False
        End If

        'create new user entry
        Dim newUserEntry = CType(CloneEntity(localContext, origUserEntry), tblUsers)
        newUserEntry.UserID = newUserID
        dbInfoEntry.tblUsers.Add(newUserEntry)

        'clone and update project entries
        For Each projectEntry In origUserEntry.tblProjects

            Dim newProjectEntry = CType(CloneEntity(localContext, projectEntry), tblProjects)
            With newProjectEntry
                .GUID = Guid.NewGuid.ToString("D")
                .UserID = newUserID
            End With
            newUserEntry.tblProjects.Add(newProjectEntry)

            'clone and update experiment entries
            For Each expEntry In projectEntry.tblExperiments

                Dim newExpEntry = CType(CloneEntity(localContext, expEntry), tblExperiments)
                With newExpEntry
                    Dim res = expEntry.ExperimentID.Split("-"c)
                    .ExperimentID = newUserID + "-" + res(1)
                    .UserID = newUserID
                    .ProjectID = newProjectEntry.GUID
                End With
                newProjectEntry.tblExperiments.Add(newExpEntry)

                'clone and update protocolItem entries
                For Each protEntry In expEntry.tblProtocolItems

                    Dim newProtEntry = CType(CloneEntity(localContext, protEntry), tblProtocolItems)
                    With newProtEntry
                        Dim res = expEntry.ExperimentID.Split("-"c)
                        .GUID = Guid.NewGuid.ToString("D")
                        .ExperimentID = newUserID + "-" + res(1)
                    End With
                    newExpEntry.tblProtocolItems.Add(newProtEntry)

                    'clone and update protocolItem child entries
                    Dim contentItem = GetContentItem(localContext, protEntry, expEntry)
                    If contentItem IsNot Nothing Then   'can be nothing for product placeholder
                        Dim newContentEntry = CloneEntity(localContext, contentItem)
                        With newContentEntry
                            .GUID = Guid.NewGuid.ToString("D")
                            .ProtocolItemID = newProtEntry.GUID
                        End With
                        localContext.Add(newContentEntry)
                    End If

                Next
            Next
        Next

        Try

            localContext.SaveChanges()

            'no exception occurred:
            dbInfoEntry.tblUsers.Remove(origUserEntry)
            localContext.SaveChangesNoSyncTracking()    'important without sync tracking, otherwise original user deleted from server!

            Return True
        Catch ex As Exception
            Return False
        End Try

    End Function


    Private Shared Function CloneEntity(localContext As ElnDbContext, srcEntity As Object) As Object

        Try

            Dim cpType = If(TypeOf srcEntity Is IProxyLazyLoader, srcEntity.GetType().BaseType, srcEntity.GetType())
            Dim newEntity = Activator.CreateInstance(cpType) 'creates an empty new object
            localContext.Entry(newEntity).CurrentValues.SetValues(srcEntity)

            Return newEntity

        Catch ex As Exception

            Return Nothing

        End Try

    End Function


    Private Shared Function GetContentItem(localContext As ElnDbContext, protocolItem As tblProtocolItems, expEntry As tblExperiments) As Object

        For Each nav In localContext.Entry(protocolItem).Navigations
            nav.Load()
            If nav.CurrentValue IsNot Nothing AndAlso nav.CurrentValue IsNot expEntry Then
                Return nav.CurrentValue
                Exit For
            End If
        Next

        Return Nothing

    End Function

End Class
