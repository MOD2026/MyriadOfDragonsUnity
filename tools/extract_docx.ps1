param([string]$Path)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($Path)
$entry = $zip.Entries | Where-Object { $_.FullName -eq 'word/document.xml' }
$sr = New-Object IO.StreamReader($entry.Open())
$xml = $sr.ReadToEnd()
$sr.Close()
$zip.Dispose()
$text = $xml -replace '<w:tab[^>]*/>', "`t" -replace '</w:p>', "`n" -replace '<[^>]+>', '' -replace '&amp;', '&' -replace '&lt;', '<' -replace '&gt;', '>'
Write-Output $text
