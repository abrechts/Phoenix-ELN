Imports System.Reflection
Imports CustomControls
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel


Public Class Users

    ''' <summary>
    ''' Converts the demo database to a non-sandbox one with a unique user, a first project and a first 
    ''' empty experiment. The materials database is retained.
    ''' </summary>
    ''' <param name="mainWdw">The current main window.</param>
    ''' <param name="expNavTree">The current experiment tree (for adding experiment)</param>
    ''' 
    Public Shared Function CreateFirstUser(mainWdw As MainWindow) As Boolean

        'Applies to Demo user only
        Dim dbEntry = MainWindow.DBContext.tblDatabaseInfo.First
        If dbEntry.tblUsers.First.UserID <> "demo" Then
            Return False
        End If

        Dim res = MsgBox("You are about to create a personal, non-demo user for " + vbCrLf +
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
                                    .Database = newDbInfo
                                End With
                                newDbInfo.tblUsers.Add(newUser)

                                '- Add first project
                                Dim newProject As New tblProjects
                                With newProject
                                    .GUID = Guid.NewGuid.ToString("D")
                                    .Title = "Project 1"
                                    .IsNodeExpanded = True
                                    .SequenceNr = 0
                                    .User = newUser
                                End With
                                newUser.tblProjects.Add(newProject)

                                '- Add first experiment
                                ExperimentBase.AddExperiment(MainWindow.DBContext, newProject, Nothing, CloneType.EmptyExperiment)

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


End Class
