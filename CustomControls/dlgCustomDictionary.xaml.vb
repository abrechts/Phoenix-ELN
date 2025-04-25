Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Public Class dlgCustomDictionary

    Private customWords As List(Of String)
    Private deletedWords As New List(Of String)

    Public Sub New(customWords As List(Of String))

        InitializeComponent()
        Me.customWords = customWords
        lstCustomWords.ItemsSource = customWords

    End Sub


    Private Sub lstCustomWords_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles lstCustomWords.SelectionChanged

        btnDelete.IsEnabled = lstCustomWords.SelectedItem IsNot Nothing

    End Sub


    Private Sub btnDelete_Click(sender As Object, e As RoutedEventArgs)

        Dim selectedWords = lstCustomWords.SelectedItems.Cast(Of String)

        If selectedWords IsNot Nothing AndAlso selectedWords.Count > 0 Then

            customWords = customWords.Except(selectedWords).ToList
            deletedWords = deletedWords.Union(selectedWords).ToList
            lstCustomWords.ItemsSource = customWords

        End If

        btnDelete.IsEnabled = lstCustomWords.SelectedItem IsNot Nothing

    End Sub


    Private Sub dlgCustomDictionary_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            lstCustomWords.SelectedItems.Clear()
            e.Handled = True
        End If

    End Sub


    Private Sub btnOK_Click(sender As Object, e As RoutedEventArgs)

        If deletedWords.Count > 0 Then
            SpellChecker.RemoveFromCustomWords(deletedWords)
        End If

        Me.Close()

    End Sub


    Private Sub btnCancel_Click(sender As Object, e As RoutedEventArgs)

        Me.Close()

    End Sub


End Class
