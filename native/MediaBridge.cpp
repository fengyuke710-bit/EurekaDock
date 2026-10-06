#define NOMINMAX
#include <windows.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Media.Control.h>
#include <winrt/Windows.Storage.Streams.h>
#include <chrono>
using namespace winrt;
using namespace Windows::Media::Control;
using namespace Windows::Storage::Streams;
template<class T> auto Await(T const& op){if(op.wait_for(std::chrono::seconds(2))==Windows::Foundation::AsyncStatus::Started){op.Cancel();throw_hresult(HRESULT_FROM_WIN32(ERROR_TIMEOUT));}return op.get();}
extern "C" __declspec(dllexport) int ReadMedia(wchar_t* title,int titleSize,wchar_t* artist,int artistSize,wchar_t* album,int albumSize,unsigned char* bytes,int capacity,int* size,int* playing){
 *size=0;*playing=0;title[0]=artist[0]=album[0]=0;
 try{
  init_apartment(apartment_type::multi_threaded);struct End{~End(){uninit_apartment();}}end;
  auto manager=Await(GlobalSystemMediaTransportControlsSessionManager::RequestAsync());
  auto session=manager.GetCurrentSession();
  // Prefer a playing session with actual song metadata over the last paused
  // session. Audio-only apps such as animated wallpapers have no song title
  // and artist, so they do not take over the cover and track labels.
  bool currentSong=false;
  if(session&&session.GetPlaybackInfo().PlaybackStatus()==GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing){
   try{auto media=Await(session.TryGetMediaPropertiesAsync());currentSong=!media.Title().empty()&&(!media.Artist().empty()||!media.AlbumArtist().empty());}catch(...){}
  }
  if(!currentSong){
   for(auto const& candidate:manager.GetSessions()){
    if(candidate.GetPlaybackInfo().PlaybackStatus()!=GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing)continue;
    try{auto media=Await(candidate.TryGetMediaPropertiesAsync());
     if(!media.Title().empty()&&(!media.Artist().empty()||!media.AlbumArtist().empty())){session=candidate;break;}
    }catch(...){/* Another session may disappear during enumeration. */}
    if(!session)session=candidate;
   }
  }
  if(!session)return 1;
  auto info=Await(session.TryGetMediaPropertiesAsync());
  wcsncpy_s(title,titleSize,info.Title().c_str(),_TRUNCATE);
  auto author=info.Artist();if(author.empty())author=info.AlbumArtist();wcsncpy_s(artist,artistSize,author.c_str(),_TRUNCATE);
  wcsncpy_s(album,albumSize,info.AlbumTitle().c_str(),_TRUNCATE);
  *playing=session.GetPlaybackInfo().PlaybackStatus()==GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing?1:0;
  try{auto thumbnail=info.Thumbnail();if(thumbnail){auto stream=Await(thumbnail.OpenReadAsync());auto length=stream.Size();if(length>0&&length<=static_cast<uint64_t>(capacity)){DataReader reader(stream);auto loaded=Await(reader.LoadAsync(static_cast<uint32_t>(length)));reader.ReadBytes(array_view<uint8_t>(bytes,bytes+loaded));*size=static_cast<int>(loaded);reader.Close();}stream.Close();}}catch(...){/* Text stays usable if artwork is unavailable. */}
  return 0;
 }catch(hresult_error const& e){return e.code();}catch(...){return E_FAIL;}
}
