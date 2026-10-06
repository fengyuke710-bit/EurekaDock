using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace EurekaDock;
internal sealed record TrackInfo(string Title,string Artist,ImageSource? Artwork,bool Playing,string Album=""){
 // Some players put "artist — album" in Artist and leave AlbumTitle empty.
 // Only use this observed delimiter as a fallback; do not split ordinary hyphens.
 internal int CombinedAlbumIndex=>string.IsNullOrWhiteSpace(Album)?Artist.IndexOf(" — ",StringComparison.Ordinal):-1;
 internal string DisplayAlbum=>!string.IsNullOrWhiteSpace(Album)?Album.Trim():CombinedAlbumIndex>0?Artist[(CombinedAlbumIndex+3)..].Trim():"";
 internal string DisplayTitle {get {string album=DisplayAlbum,title=Title.Trim();if(album.Length==0||title.Equals(album,StringComparison.OrdinalIgnoreCase))return title;
  foreach(string separator in new[]{" · "," — "," - "," | "," • "," – "})if(title.EndsWith(separator+album,StringComparison.OrdinalIgnoreCase))return title;
  return title.Length==0?album:title+" · "+album;}}
 internal string DisplayArtist {get {string album=DisplayAlbum,artist=Artist.Trim();
  if(CombinedAlbumIndex>0&&album.Length>0)return Artist[..CombinedAlbumIndex].Trim();
  foreach(string separator in new[]{" · "," — "," - "," | "," • "," – "}){string suffix=separator+album;if(album.Length>0&&artist.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return artist[..^suffix.Length].Trim();}return artist;}}
 internal bool Available=>Title.Length>0||Artist.Length>0||Artwork!=null;
 internal static readonly TrackInfo Empty=new("","",null,false);
}
internal sealed class MediaInfoReader:IDisposable{
 [DllImport("EurekaDock.Media.dll",CharSet=CharSet.Unicode)]static extern int ReadMedia(StringBuilder title,int titleSize,StringBuilder artist,int artistSize,StringBuilder album,int albumSize,byte[] bytes,int capacity,out int size,out int playing);
 readonly ManualResetEvent stop=new(false);readonly Thread thread;readonly Action<TrackInfo> consume;int disposed;
 internal MediaInfoReader(Action<TrackInfo> callback){consume=callback;thread=new Thread(()=>{try{Run();}finally{stop.Dispose();}}){IsBackground=true,Name="EurekaDock media info"};thread.Start();}
 internal static (int result,TrackInfo track) Read(){
  var title=new StringBuilder(2048);var artist=new StringBuilder(2048);var album=new StringBuilder(2048);var bytes=new byte[4*1024*1024];
  int result=ReadMedia(title,title.Capacity,artist,artist.Capacity,album,album.Capacity,bytes,bytes.Length,out int size,out int playing);
  ImageSource? artwork=null;
  if(size>0)try{using var stream=new MemoryStream(bytes,0,size);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=256;image.StreamSource=stream;image.EndInit();image.Freeze();artwork=image;}catch{}
  return (result,new TrackInfo(title.ToString(),artist.ToString(),artwork,playing!=0,album.ToString()));
 }
 void Run(){
  string previous="";int previousError=0;TrackInfo retained=TrackInfo.Empty;var bytes=new byte[4*1024*1024];var title=new StringBuilder(2048);var artist=new StringBuilder(2048);var album=new StringBuilder(2048);
  while(!stop.WaitOne(0)){
   try{
    int result=ReadMedia(title,title.Capacity,artist,artist.Capacity,album,album.Capacity,bytes,bytes.Length,out int size,out int playing);
    if(result<0&&result!=previousError){App.Store.Log("Media info: 0x"+result.ToString("X8"));previousError=result;}
    if(result>=0)previousError=0;
    string key=result+"|"+title+"|"+artist+"|"+album+"|"+playing+"|"+Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0,size)));
    if(key!=previous){
     ImageSource? artwork=null;
     if(size>0)try{using var stream=new MemoryStream(bytes,0,size);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=256;image.StreamSource=stream;image.EndInit();image.Freeze();artwork=image;}catch(Exception ex){App.Store.Log("Media artwork: "+ex.Message);}
     if(result==0){var current=new TrackInfo(title.ToString(),artist.ToString(),artwork,playing!=0,album.ToString());retained=current.Available?current:retained with {Playing=false};}
     else retained=retained with {Playing=false};
     if(!stop.WaitOne(0))consume(retained);previous=key;
    }
   }catch(Exception ex){if(previousError!=-1){App.Store.Log("Media info: "+ex.Message);previousError=-1;}if(!stop.WaitOne(0))consume(retained);}
   if(stop.WaitOne(2000))break;
  }
 }
 public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Set();thread.Join(100);}
}
