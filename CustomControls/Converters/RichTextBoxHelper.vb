Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents

''' <summary>
''' Provides RichTextBox databinding support for FlowDocs stored as XAML, according to 
''' https://stackoverflow.com/questions/343468/richtextbox-wpf-binding
''' </summary>
''' 
Public Class RichTextBoxHelper

    Inherits DependencyObject

    Private Shared _recursionProtection As New HashSet(Of System.Threading.Thread)()

    Public Shared Function GetDocumentXaml(ByVal depObj As DependencyObject) As String

        Return DirectCast(depObj.GetValue(DocumentXamlProperty), String)

    End Function


    Public Shared Sub SetDocumentXaml(ByVal depObj As DependencyObject, ByVal value As String)

        _recursionProtection.Add(System.Threading.Thread.CurrentThread)
        depObj.SetValue(DocumentXamlProperty, value)
        _recursionProtection.Remove(System.Threading.Thread.CurrentThread)

    End Sub


    Public Shared ReadOnly DocumentXamlProperty As DependencyProperty =
       DependencyProperty.RegisterAttached("DocumentXaml", GetType(String), GetType(RichTextBoxHelper),
       New FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender Or FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
       Sub(depObj, e)
           RegisterIt(depObj, e)
       End Sub))


    Private Shared Sub RegisterIt(ByVal depObj As System.Windows.DependencyObject, ByVal e As System.Windows.DependencyPropertyChangedEventArgs)

        If _recursionProtection.Contains(System.Threading.Thread.CurrentThread) Then
            Return
        End If

        Dim rtb As RichTextBox = DirectCast(depObj, RichTextBox)

        Try
            rtb.Document = Markup.XamlReader.Parse(GetDocumentXaml(rtb))
        Catch
            rtb.Document = New FlowDocument()
        End Try

        ' When the document changes update the source
        AddHandler rtb.TextChanged, AddressOf TextChanged

    End Sub


    Private Shared Sub TextChanged(ByVal sender As Object, ByVal e As TextChangedEventArgs)

        Dim rtb As RichTextBox = TryCast(sender, RichTextBox)
        If rtb IsNot Nothing Then
            SetDocumentXaml(sender, Markup.XamlWriter.Save(rtb.Document))
        End If

    End Sub

End Class

