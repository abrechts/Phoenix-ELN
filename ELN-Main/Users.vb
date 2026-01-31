Imports CustomControls
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore.Proxies.Internal


Public Class Users

    ''' <summary>
    ''' Converts the demo database to a non-sandbox one with a unique user, a first project and a first 
    ''' empty experiment. The materials database is retained.
    ''' </summary>
    ''' <param name="mainWdw">The current main window.</param>
    ''' 
    Public Shared Function CreateFirstUser(mainWdw As MainWindow) As Boolean

        'Applies to Demo user only
        Dim dbEntry = MainWindow.DBContext.tblDatabaseInfo.First
        If dbEntry.tblUsers.First.UserID <> "demo" Then
            Return False
        End If

        Dim res = cbMsgBox.Display("You are about to create a personal, non-demo user for " + vbCrLf +
                        "recording your 'real' experiments. - Please note that" + vbCrLf +
                        "this will delete all of your current 'demo' user" + vbCrLf +
                        "sandbox experiments! " + vbCrLf + vbCrLf +
                        "If you'd like to keep some of your demo experiments," + vbCrLf +
                        "best save them as PDF, or print them before proceeding." + vbCrLf + vbCrLf +
                        "Click OK when you are ready to create the new user.",
                        MsgBoxStyle.Information + MsgBoxStyle.OkCancel + MsgBoxStyle.DefaultButton2, "New User")

        If res = MsgBoxResult.Ok Then

            'Run new user wizard
            Dim newUserDlg As New dlgNewUser(MainWindow.DBContext, MainWindow.ServerDBContext)

            With newUserDlg
                .Owner = mainWdw
                If .ShowDialog Then

                    'User confirms: Display password dialog
                    Dim passwordDlg As New dlgPassword(Nothing)
                    With passwordDlg
                        .Title = "Password"
                        .Owner = mainWdw

                        If .ShowDialog() Then

                            With MainWindow.DBContext

                                'save a copy of the default materials list (preset materials only)
                                Dim matList = (From mat In dbEntry.tblMaterials Where mat.IsValidated = MaterialValidation.Preset).ToList

                                '*delete* dbInfo, i.e. every content on database via cascading deletes
                                .tblDatabaseInfo.Remove(.tblDatabaseInfo.First)
                                .SaveChangesNoSyncTracking()

                                'create new dbInfo entry
                                Dim newDbInfo As New tblDatabaseInfo
                                With newDbInfo
                                    .GUID = Guid.NewGuid.ToString("D")
                                    .CurrAppVersion = MainWindow.ApplicationVersion.ToString
                                End With
                                .tblDatabaseInfo.Add(newDbInfo)
                                .SaveChanges()

                                'restore materials with new GUID
                                For Each mat In matList
                                    Dim matEntry As New tblMaterials
                                    With matEntry
                                        .GUID = Guid.NewGuid.ToString("D")
                                        .Database = newDbInfo
                                        .MatType = mat.MatType
                                        .MatName = mat.MatName
                                        .MatSource = mat.MatSource
                                        .Molweight = mat.Molweight
                                        .Molarity = mat.Molarity
                                        .Density = mat.Density
                                        .Purity = mat.Purity
                                        .IsValidated = mat.IsValidated
                                    End With
                                    newDbInfo.tblMaterials.Add(matEntry)
                                Next

                                ProtocolItemBase.DbInfo = newDbInfo

                                '- Create the new user data
                                Dim newUser As New tblUsers
                                With newUser
                                    .UserID = newUserDlg.txtUserID.Text
                                    .PWHash = passwordDlg.NewPasswordHash
                                    .PWHint = passwordDlg.NewPasswordHint
                                    .FirstName = newUserDlg.txtFirstName.Text
                                    .LastName = newUserDlg.txtLastName.Text
                                    .CompanyName = newUserDlg.txtOrganization.Text
                                    .City = newUserDlg.txtSite.Text
                                    .DepartmentName = newUserDlg.txtDepartment.Text
                                    .SequenceNr = 0  'is default field value, but assign anyway
                                    .Database = newDbInfo
                                End With
                                newDbInfo.tblUsers.Add(newUser)

                                '- Add first project
                                Dim newProject As New tblProjects
                                With newProject
                                    .GUID = Guid.NewGuid.ToString("D")
                                    .Title = "Project 1"
                                    .IsNodeExpanded = 1
                                    .SequenceNr = 0
                                    .User = newUser
                                End With
                                newUser.tblProjects.Add(newProject)

                                Dim projFolder = ProjectFolders.Add(newProject, ProjectFolders.DefaultFolderTitle, MainWindow.DBContext)

                                '- Add first experiment
                                Dim newExp = ExperimentBase.AddExperiment(MainWindow.DBContext, newProject, projFolder, Nothing, CloneType.EmptyExperiment)
                                newExp.ProjFolder = projFolder

                                '-Save all changes
                                .SaveChanges()

                                '- Update data context
                                mainWdw.DataContext = Nothing
                                mainWdw.DataContext = newUser

                                Return True

                            End With

                        End If
                    End With
                End If
            End With
        End If

        Return False

    End Function


    ''' <summary>
    ''' Adds a new user to the current experiments database. This is useful for accommodating additional ELN users
    ''' on your own machine, or when working in multiple groups in parallel.
    ''' </summary>
    ''' <param name="mainWdw">The main window</param>
    ''' <returns>New user entry if successful, otherwise nothing.</returns>
    ''' 
    Public Shared Function CreateAdditionalUser(mainWdw As MainWindow) As tblUsers

        'Applies to Non-Demo user only
        Dim dbEntry = MainWindow.DBContext.tblDatabaseInfo.First
        If dbEntry.tblUsers.First.UserID = "demo" Then
            Return Nothing
        End If

        Dim res = cbMsgBox.Display("This will add another user with its own userID to " + vbCrLf +
                        "your local ELN database." + vbCrLf + vbCrLf +
                        "This is useful when sharing your machine with other" + vbCrLf +
                        "ELN users, or if you are working in multiple" + vbCrLf +
                        "work environments requiring separate userID's.",
                        MsgBoxStyle.Information + MsgBoxStyle.OkCancel + MsgBoxStyle.DefaultButton2, "New User")

        If res = MsgBoxResult.Ok Then

            'Run new user wizard
            Dim newUserDlg As New dlgNewUser(MainWindow.DBContext, MainWindow.ServerDBContext)

            With newUserDlg
                .Owner = mainWdw
                If .ShowDialog Then

                    'User confirms: Display password dialog
                    Dim passwordDlg As New dlgPassword(Nothing)
                    With passwordDlg
                        .Title = "Password"
                        .Owner = mainWdw

                        If .ShowDialog() Then

                            'get highest user sequence number
                            Dim maxUserSeqNr = Aggregate user In dbEntry.tblUsers Into Max(user.SequenceNr)

                            With MainWindow.DBContext

                                '- Create the new user data
                                Dim newUser As New tblUsers
                                With newUser
                                    .UserID = newUserDlg.txtUserID.Text
                                    .PWHash = passwordDlg.NewPasswordHash
                                    .PWHint = passwordDlg.NewPasswordHint
                                    .FirstName = newUserDlg.txtFirstName.Text
                                    .LastName = newUserDlg.txtLastName.Text
                                    .CompanyName = newUserDlg.txtOrganization.Text
                                    .City = newUserDlg.txtSite.Text
                                    .DepartmentName = newUserDlg.txtDepartment.Text
                                    .SequenceNr = maxUserSeqNr + 1
                                    .Database = dbEntry
                                End With
                                dbEntry.tblUsers.Add(newUser)

                                '- Add first project
                                Dim newProject As New tblProjects
                                With newProject
                                    .GUID = Guid.NewGuid.ToString("D")
                                    .Title = "Project 1"
                                    .IsNodeExpanded = 1
                                    .SequenceNr = 0
                                    .User = newUser
                                End With
                                newUser.tblProjects.Add(newProject)

                                Dim projFolder = ProjectFolders.Add(newProject, ProjectFolders.DefaultFolderTitle, MainWindow.DBContext)

                                '- Add first experiment
                                Dim newExp = ExperimentBase.AddExperiment(MainWindow.DBContext, newProject, projFolder, Nothing, CloneType.EmptyExperiment)
                                newExp.ProjFolder = projFolder

                                '-Save all changes
                                .SaveChanges()

                                Return newUser

                            End With

                        End If
                    End With
                End If
            End With
        End If

        Return Nothing

    End Function


    ''' <summary>
    ''' Increases the sequence numbers of the local experiment-IDs of the specified user to allow seamless merge 'on top' of his 
    ''' already existing server experiments and adds newly added local projects, followed by a restore from server. Typically 
    ''' used when the user recreated his userID from scratch after the migration to a new PC, instead of restoring from the server.
    ''' </summary>
    ''' 
    Public Shared Function MergeConflictingUserExp(localUser As tblUsers, localContext As ElnDbContext, serverContext As ElnDbContext) As Boolean

        'renumber local duplicate experiment-ID's to add them 'on top' of existing server experiments after merge sync

        Dim maxLocalExpID = (From exp In localUser.tblExperiments Select exp.ExperimentID Order By ExperimentID Descending).First
        Dim maxLocalIndex = maxLocalExpID.Split("-"c)(1)
        Dim maxServerExpID = (From exp In serverContext.tblExperiments Where exp.UserID = localUser.UserID Select exp.ExperimentID
                              Order By ExperimentID Descending).First
        Dim maxServerIndex = maxServerExpID.Split("-"c)(1)
        Dim serverProjCount = (From proj In serverContext.tblProjects Where proj.UserID = localUser.UserID).Count

        For Each projectEntry In localUser.tblProjects

            Dim serverProjEntry = (From proj In serverContext.tblProjects Where proj.UserID = localUser.UserID _
                                   AndAlso proj.Title = projectEntry.Title).FirstOrDefault 'user project titles are unique

            'project not yet present on server? -> clone one
            If serverProjEntry Is Nothing Then
                serverProjEntry = CType(CloneEntity(localContext, projectEntry), tblProjects)
                With serverProjEntry
                    .GUID = Guid.NewGuid.ToString("D")
                    .SequenceNr = serverProjCount   'add new server project to top
                End With
                serverContext.Add(serverProjEntry)
            End If

            'reset all existing project experiments current/pinned properties
            Dim currExpList = From exp In serverProjEntry.tblExperiments Where exp.IsCurrent
            For Each expEntry In currExpList
                expEntry.IsCurrent = 0
                expEntry.DisplayIndex = Nothing
            Next

            'clone and update experiment entries
            For Each expEntry In projectEntry.tblExperiments

                Dim res = expEntry.ExperimentID.Split("-"c)
                Dim newExpID = res(0) + "-" + Format(CInt(res(1)) + maxServerIndex, "00000")

                Dim newExpEntry = CType(CloneEntity(localContext, expEntry), tblExperiments)
                With newExpEntry
                    .ExperimentID = newExpID
                    .ProjectID = serverProjEntry.GUID
                    .IsCurrent = 0
                End With

                'TODO -- replace LOCAL projEntry by SERVER projEntry
                serverProjEntry.tblExperiments.Add(newExpEntry)

                'clone and update protocolItem entries
                For Each protEntry In expEntry.tblProtocolItems

                    Dim newProtEntry = CType(CloneEntity(localContext, protEntry), tblProtocolItems)
                    With newProtEntry
                        .GUID = Guid.NewGuid.ToString("D")
                        .ExperimentID = newExpID
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
                        serverContext.Add(newContentEntry)
                    End If

                Next
            Next
        Next

        Try

            serverContext.SaveChanges()
            Return True

        Catch ex As Exception

            Return False

        End Try

    End Function



    ''' <summary>
    ''' Replaces the specified userID original userID by the new userID in all user experiment-ID's in the user entry 
    ''' and in the related project items.
    ''' </summary>
    ''' <param name="localContext">The local ElbDbContext</param>
    ''' <param name="dupLocalUser">The local user entry to be renamed.</param>
    ''' <param name="newUserID">The new userID, which needs to be checked for server duplicates before.</param>
    ''' <returns>False, if a database save error occurred after completed renaming.</returns>
    '''
    Public Shared Function ReplaceUserID(localContext As ElnDbContext, dupLocalUser As tblUsers, newUserID As String) As Boolean

        If dupLocalUser.UserID.ToLower = newUserID.ToLower Then
            Return True 'nothing to do, but no error
        End If

        Dim dbInfoEntry = localContext.tblDatabaseInfo.First

        'create new user entry
        Dim newUserEntry = CType(CloneEntity(localContext, dupLocalUser), tblUsers)
        newUserEntry.UserID = newUserID
        dbInfoEntry.tblUsers.Add(newUserEntry)

        'clone and update project entries
        For Each projectEntry In dupLocalUser.tblProjects

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
            dbInfoEntry.tblUsers.Remove(dupLocalUser)
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
