
Imports System.Runtime.InteropServices
Imports System.Drawing
Imports System.IO
Imports System.Windows.Media.Imaging

''' <summary>
''' Extracts an icon of a specified size from a file.
''' </summary>
''' <remarks>This class needed to be implemented since Icon.ExctractAssociatedIcon only returns 32x32 icons,
''' but not 16x16 ones (status .Net 2.0)</remarks>

Public Class IconExtract

    ''' <summary>
    ''' Specifies the size of an icon to be extracted.
    ''' </summary>
    ''' <remarks></remarks>
    <Flags()> _
    Public Enum IconFlags As Integer
        ''' <summary>Extract large file icon (32 x 32).</summary>
        iconLarge = &H0
        ''' <summary>Extract small file icon (16 x 16).</summary>
        iconSmall = &H1
        'iconOpen = &H2 'Open Folder
        'iconLinkOverlay = &H8000
        'iconSelected = &H10000
    End Enum

    Private Const SHGFI_ATTRIBUTES As Integer = &H800
    Private Const SHGFI_DISPLAYNAME As Integer = &H200
    Private Const SHGFI_EXETYPE As Integer = &H2000
    Private Const SHGFI_ICON As Integer = &H100
    Private Const SHGFI_ICONLOCATION As Integer = &H1000
    Private Const SHGFI_LARGEICON As Integer = &H0
    Private Const SHGFI_LINKOVERLAY As Integer = 32768 '= &H8000 
    Private Const SHGFI_OPENICON As Integer = &H2
    Private Const SHGFI_PIDL As Integer = &H8
    Private Const SHGFI_SELECTED As Integer = &H10000
    Private Const SHGFI_SHELLICONSIZE As Integer = &H4
    Private Const SHGFI_SMALLICON As Integer = &H1
    Private Const SHGFI_SYSICONINDEX As Integer = &H4000
    Private Const SHGFI_TYPENAME As Integer = &H400
    Private Const SHGFI_USEFILEATTRIBUTES As Integer = &H10

    <StructLayout(LayoutKind.Sequential, Pack:=1, CharSet:=CharSet.Auto)> _
    Private Structure SHFILEINFO
        Public hIcon As IntPtr ' : icon 
        Public iIcon As Integer ' : icoIndex 
        Public dwAttributes As Integer ' : SFGAO_ flags 
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)> Public szDisplayName As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=80)> Public szTypeName As String
    End Structure

    <DllImportAttribute("Shell32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SHGetFileInfo(ByVal pszPath As String, ByVal _
    dwFileAttributes As Integer, ByRef psfi As SHFILEINFO, ByVal cbFileInfo As _
    Integer, ByVal uFlags As Integer) As IntPtr

    End Function

    <DllImportAttribute("User32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function DestroyIcon(ByVal hIcon As IntPtr) As Integer
    End Function


    Public Shared Function BitmapImageFromFile(ByVal FileName As String, ByVal Options As IconFlags) As BitmapImage

        Dim ico = IconFromFile(FileName, Options)

        Using ico

            Dim bmp As Bitmap = ico.ToBitmap()
            Dim strm As New MemoryStream()
            bmp.Save(strm, System.Drawing.Imaging.ImageFormat.Png)
            Dim bmpImage As New BitmapImage()
            bmpImage.BeginInit()
            strm.Seek(0, SeekOrigin.Begin)
            bmpImage.StreamSource = strm
            bmpImage.EndInit()

            Return bmpImage

        End Using

    End Function



    ''' <summary>
    ''' Retrieves the icon of the specified file.
    ''' </summary>
    ''' <param name="FileName"></param>
    ''' <param name="Options">One of the <see cref="IconFlags"></see> paramters which determine 
    ''' the size of the icon to be extracted.</param>
    ''' <returns>File icon.</returns>
    ''' <remarks></remarks>
    '''
    Public Shared Function IconFromFile(ByVal FileName As String, ByVal Options As IconFlags) As Icon

        If String.IsNullOrEmpty(FileName) OrElse (Not IO.File.Exists(FileName)) Then
            Throw New IO.FileNotFoundException("File not found!", FileName)
            Return Nothing
        End If

        Dim fi As New SHFILEINFO
        Dim hImg As IntPtr = IntPtr.Zero
        Dim iconObj As Object
        Dim Icon1 As Icon = Nothing

        Try
            hImg = SHGetFileInfo(FileName, 0, fi, Marshal.SizeOf(fi), SHGFI_ICON Or Options)
            'make copy of icon
            iconObj = System.Drawing.Icon.FromHandle(fi.hIcon).Clone
            Icon1 = TryCast(iconObj, System.Drawing.Icon)
        Finally
            DestroyIcon(fi.hIcon)
        End Try

        Return Icon1

    End Function




End Class


