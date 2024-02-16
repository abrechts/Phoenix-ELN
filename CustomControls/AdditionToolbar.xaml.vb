Imports System.Windows
Imports System.Windows.Controls

Public Class AdditionToolbar

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets if the a new protocol item should be inserted below the currently selected 
    ''' one (true), or appended after the last protocol element (false). 
    ''' </summary>
    ''' 
    Public Shared Property DoInsert As Boolean = False


    ''' <summary>
    ''' Sets or gets the protocol this toolbar is assigned to. 
    ''' </summary>
    '''
    Public Property ParentProtocol As Protocol


    'Handle menu selections

    Private Sub cboAddMat_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles cboAddMat.SelectionChanged

        If cboAddMat.SelectedIndex > 0 Then

            Select Case cboAddMat.SelectedItem.name
                Case "AddReactant"
                    ParentProtocol.AddRefReactant()

                Case "AddReagent"
                    ParentProtocol.AddReagent()

                Case "AddSolvent"
                    ParentProtocol.AddSolvent()

                Case "AddAuxiliary"
                    ParentProtocol.AddAuxiliary()

                Case "AddProduct"
                    ParentProtocol.AddProduct()

            End Select

        End If

    End Sub

    Private Sub btnAddComment_Click() Handles btnAddComment.Click
        ParentProtocol.AddComment()
    End Sub


    Private Sub cboAddMat_MouseEnter() Handles cboAddMat.MouseEnter
        cboAddMat.IsDropDownOpen = True
    End Sub


    ''' <summary>
    ''' Prevents auto-dropdown deactivation when clicking button directly
    ''' </summary>
    '''
    Private Sub cboAddMat_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles cboAddMat.PreviewMouseDown
        e.Handled = True
    End Sub


    Private Sub cboAddMat_DropDownOpen() Handles cboAddMat.DropDownOpened
        cboAddMat.Items(0).Visibility = Visibility.Collapsed
    End Sub

    Private Sub cboAddMat_DropDownClosed() Handles cboAddMat.DropDownClosed

        cboAddMat.Items(0).Visibility = Visibility.Visible
        cboAddMat.SelectedIndex = 0

    End Sub


    Private Sub chkInsert_Changed() Handles chkInsert.Checked, chkInsert.Unchecked

        DoInsert = chkInsert.IsChecked

    End Sub

    ''' <summary>
    ''' Close auto-opened PopUp
    ''' </summary>
    ''' 
    Private Sub Me_MouseMove(sender As Object, e As System.Windows.Input.MouseEventArgs) Handles Me.PreviewMouseMove

        With cboAddMat
            Dim p As Primitives.Popup = DirectCast(.Template.FindName("PART_Popup", cboAddMat), Primitives.Popup)
            If .IsDropDownOpen AndAlso Not p.IsMouseOver AndAlso .IsMouseDirectlyOver Then
                .IsDropDownOpen = False
            End If
        End With

        With cboAddOther
            Dim p As Primitives.Popup = DirectCast(.Template.FindName("PART_Popup", cboAddOther), Primitives.Popup)
            If .IsDropDownOpen AndAlso Not p.IsMouseOver AndAlso .IsMouseDirectlyOver Then
                .IsDropDownOpen = False
            End If
        End With

    End Sub


    Private Sub cboAddOther_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles cboAddOther.SelectionChanged

        If cboAddOther.SelectedIndex > 0 Then

            Select Case cboAddOther.SelectedItem.Name

                Case "AddSeparator"
                    ParentProtocol.AddSeparator()

                Case "AddFile"
                    ParentProtocol.AddFiles()

            End Select

        End If

    End Sub


    Private Sub cboAddOther_MouseEnter() Handles cboAddOther.MouseEnter
        cboAddOther.IsDropDownOpen = True
    End Sub


    ''' <summary>
    ''' Prevents auto-dropdown deactivation when clicking button directly
    ''' </summary>
    '''
    Private Sub cboAddOther_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles cboAddOther.PreviewMouseDown
        e.Handled = True
    End Sub

    Private Sub cboAddOther_DropDownOpen() Handles cboAddOther.DropDownOpened
        cboAddOther.Items(0).Visibility = Visibility.Collapsed
    End Sub

    Private Sub cboAddOther_DropDownClosed() Handles cboAddOther.DropDownClosed

        cboAddOther.Items(0).Visibility = Visibility.Visible
        cboAddOther.SelectedIndex = 0

    End Sub


End Class
