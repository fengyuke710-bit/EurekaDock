#define NOMINMAX
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <commctrl.h>
#include <dwmapi.h>
#include <wrl/client.h>
#include <string>
#include <exdisp.h>
#include <shlguid.h>
using Microsoft::WRL::ComPtr;
#define API extern "C" __declspec(dllexport)

API void OpenExistingSettings() {
 EnumWindows([](HWND h,LPARAM)->BOOL {
  wchar_t title[128]{};GetWindowText(h,title,128);
  if(wcscmp(title,L"EurekaDock")!=0)return TRUE;
  DWORD pid=0;GetWindowThreadProcessId(h,&pid);
  HANDLE process=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);
  if(!process)return TRUE;
  wchar_t path[32768]{},self[32768]{};DWORD length=32768;
  bool match=QueryFullProcessImageName(process,0,path,&length)&&GetModuleFileName(nullptr,self,32768)&&_wcsicmp(path,self)==0;
  CloseHandle(process);
  if(match){AllowSetForegroundWindow(pid);PostMessage(h,WM_APP+2,0,0);return FALSE;}return TRUE;
 },0);
}

// Resource extraction has no Shell shortcut/UAC overlay. Keep the original
// .lnk for launching so its arguments, working directory and elevation survive.
API HICON ReadCleanIcon(const wchar_t* path,int size) {
 std::wstring resource=path;int index=0;
 if(resource.size()>4&&_wcsicmp(resource.c_str()+resource.size()-4,L".lnk")==0) {
  ComPtr<IShellLink> link;ComPtr<IPersistFile> file;
  if(FAILED(CoCreateInstance(CLSID_ShellLink,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link)))||FAILED(link.As(&file))||FAILED(file->Load(path,STGM_READ)))return nullptr;
  wchar_t icon[32768]{},expanded[32768]{};
  link->GetIconLocation(icon,32768,&index);
  if(!icon[0]){link->GetPath(icon,32768,nullptr,SLGP_RAWPATH);index=0;}
  if(!icon[0])return nullptr;
  ExpandEnvironmentStrings(icon,expanded,32768);resource=expanded;
 }
 HICON icon=nullptr;
 if(SUCCEEDED(SHDefExtractIcon(resource.c_str(),index,0,&icon,nullptr,MAKELONG(size,size)))&&icon)return icon;
 // Extract directly from the shell folder's IExtractIcon, without an image
 // factory compositing its overlays (also covers Store/shell namespace links).
 PIDLIST_ABSOLUTE pidl=nullptr;
 if(FAILED(SHParseDisplayName(path,nullptr,&pidl,0,nullptr)))return nullptr;
 ComPtr<IShellFolder> parent;PCUITEMID_CHILD child=nullptr;ComPtr<IExtractIconW> extractor;
 if(SUCCEEDED(SHBindToParent(pidl,IID_PPV_ARGS(&parent),&child))&&SUCCEEDED(parent->GetUIObjectOf(nullptr,1,&child,IID_IExtractIconW,nullptr,reinterpret_cast<void**>(extractor.GetAddressOf())))) {
  wchar_t location[32768]{};UINT flags=0;int which=0;
  if(SUCCEEDED(extractor->GetIconLocation(GIL_FORSHELL,location,32768,&which,&flags))) {
   HICON smallIcon=nullptr;HRESULT hr=extractor->Extract(location,which,&icon,&smallIcon,MAKELONG(size,size));
   if(smallIcon&&smallIcon!=icon)DestroyIcon(smallIcon);
   if(hr==S_FALSE&&!(flags&GIL_NOTFILENAME))SHDefExtractIcon(location,which,0,&icon,nullptr,MAKELONG(size,size));
  }
 }
 CoTaskMemFree(pidl);return icon;
}

// Query (-1), hide (0), show (1), or toggle (2) the existing desktop view.
// Changes only the view flag, never deletes/moves desktop files.
API int DesktopIcons(int action) {
 ComPtr<IShellWindows> windows;
 HRESULT hr=CoCreateInstance(CLSID_ShellWindows,nullptr,CLSCTX_LOCAL_SERVER,IID_PPV_ARGS(&windows));
 if(FAILED(hr))return hr;
 VARIANT location{},empty{};location.vt=VT_I4;location.lVal=CSIDL_DESKTOP;
 long hwnd=0;ComPtr<IDispatch> dispatch;
 hr=windows->FindWindowSW(&location,&empty,SWC_DESKTOP,&hwnd,SWFO_NEEDDISPATCH,&dispatch);
 if(FAILED(hr)||!dispatch)return FAILED(hr)?hr:E_NOINTERFACE;
 ComPtr<IServiceProvider> provider;hr=dispatch.As(&provider);if(FAILED(hr))return hr;
 ComPtr<IShellBrowser> browser;hr=provider->QueryService(SID_STopLevelBrowser,IID_PPV_ARGS(&browser));if(FAILED(hr))return hr;
 ComPtr<IShellView> shellView;hr=browser->QueryActiveShellView(&shellView);if(FAILED(hr))return hr;
 ComPtr<IFolderView2> view;hr=shellView.As(&view);if(FAILED(hr))return hr;
 DWORD flags=0;hr=view->GetCurrentFolderFlags(&flags);if(FAILED(hr))return hr;
 if(action>=0) {
  bool visible=action==2?(flags&FWF_NOICONS)!=0:action==1;
  hr=view->SetCurrentFolderFlags(FWF_NOICONS,visible?0:FWF_NOICONS);if(FAILED(hr))return hr;
  hr=view->GetCurrentFolderFlags(&flags);if(FAILED(hr))return hr;
 }
 return (flags&FWF_NOICONS)?0:1;
}

// Never manufacture a WorkerW or alter Explorer's window tree.
// This probe reports existing desktop hosts; production parenting is gated by tests.
API HWND DesktopHost() {
 HWND result = nullptr;
 EnumWindows([](HWND h, LPARAM p)->BOOL {
  if (FindWindowEx(h, nullptr, L"SHELLDLL_DefView", nullptr)) {
   *reinterpret_cast<HWND*>(p)=h; return FALSE;
  } return TRUE;
 }, reinterpret_cast<LPARAM>(&result));
 return result;
}
API BOOL IsDesktopForeground() {
 HWND fg = GetForegroundWindow();
 if (!fg) return FALSE;
 wchar_t name[128]{}; GetClassName(fg,name,128);
 return wcscmp(name,L"Progman")==0 || wcscmp(name,L"WorkerW")==0;
}
API void PlaceAtBottom(HWND h) {
 SetWindowPos(h, HWND_BOTTOM,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);
}
API int AttachDesktop(HWND h) {
 HWND host=DesktopHost();
 if(!host) return ERROR_NOT_FOUND;
 // Preserve a top-level layered HWND. WPF rendered offscreen but did not
 // composite visibly when reparented as an Explorer child on this machine.
 // Ownership groups the Dock above the desktop while HWND_BOTTOM keeps it
 // beneath ordinary applications; this does not change DPI awareness.
 SetLastError(0); SetWindowLongPtr(h,GWLP_HWNDPARENT,reinterpret_cast<LONG_PTR>(host));
 DWORD error=GetLastError();
 if(error) return error;
 SetWindowPos(h,HWND_BOTTOM,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE|SWP_FRAMECHANGED|SWP_NOOWNERZORDER);
 return GetWindow(h,GW_OWNER)==host?0:ERROR_INVALID_WINDOW_HANDLE;
}
API void DetachDesktop(HWND h) {
 SetWindowLongPtr(h,GWLP_HWNDPARENT,0);
}
API int WindowDpiMode(HWND h) {return GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(h));}
typedef void (__stdcall *AppVisitor)(const wchar_t*,const wchar_t*);
API int EnumerateApps(AppVisitor visit) {
 ComPtr<IShellItem> folder;
 HRESULT hr=SHCreateItemFromParsingName(L"shell:AppsFolder",nullptr,IID_PPV_ARGS(&folder));
 if(FAILED(hr)) return hr;
 ComPtr<IEnumShellItems> enumerator;
 hr=folder->BindToHandler(nullptr,BHID_EnumItems,IID_PPV_ARGS(&enumerator));
 if(FAILED(hr)) return hr;
 int count=0; ComPtr<IShellItem> item;
 while(enumerator->Next(1,&item,nullptr)==S_OK) {
  PWSTR name=nullptr,path=nullptr;
  if(SUCCEEDED(item->GetDisplayName(SIGDN_NORMALDISPLAY,&name))&&SUCCEEDED(item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING,&path))) {
   std::wstring target=path;
   if(target.find(L'!')!=std::wstring::npos&&target.find(L"shell:")!=0&&target.find(L"::{")!=0) target=L"shell:AppsFolder\\"+target;
   visit(name,target.c_str());count++;
  }
  CoTaskMemFree(name);CoTaskMemFree(path);item.Reset();
 }
 return count;
}
API int CreateTestLink(const wchar_t* target,const wchar_t* path) {
 ComPtr<IShellLink> link; HRESULT hr=CoCreateInstance(CLSID_ShellLink,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link));
 if(FAILED(hr)) return hr;
 hr=link->SetPath(target); if(FAILED(hr)) return hr;
 ComPtr<IPersistFile> file; hr=link.As(&file);return FAILED(hr)?hr:file->Save(path,TRUE);
}
API int WriteStartupLink(const wchar_t* target,const wchar_t* args,const wchar_t* directory,const wchar_t* path) {
 ComPtr<IShellLink> link;HRESULT hr=CoCreateInstance(CLSID_ShellLink,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link));
 if(FAILED(hr))return hr;
 hr=link->SetPath(target);if(FAILED(hr))return hr;
 hr=link->SetArguments(args);if(FAILED(hr))return hr;
 hr=link->SetWorkingDirectory(directory);if(FAILED(hr))return hr;
 hr=link->SetDescription(L"EurekaDock - start with the current configuration");if(FAILED(hr))return hr;
 ComPtr<IPersistFile> file;hr=link.As(&file);return FAILED(hr)?hr:file->Save(path,TRUE);
}
API int ReadStartupLink(const wchar_t* path,wchar_t* target,int targetSize,wchar_t* args,int argsSize,wchar_t* directory,int directorySize) {
 ComPtr<IShellLink> link;HRESULT hr=CoCreateInstance(CLSID_ShellLink,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link));
 if(FAILED(hr))return hr;
 ComPtr<IPersistFile> file;hr=link.As(&file);if(FAILED(hr))return hr;
 hr=file->Load(path,STGM_READ);if(FAILED(hr))return hr;
 hr=link->GetPath(target,targetSize,nullptr,SLGP_RAWPATH);if(FAILED(hr))return hr;
 hr=link->GetArguments(args,argsSize);if(FAILED(hr))return hr;
 return link->GetWorkingDirectory(directory,directorySize);
}
API HBITMAP ReadIcon(const wchar_t* path, int size) {
 ComPtr<IShellItemImageFactory> item;
 if (FAILED(SHCreateItemFromParsingName(path,nullptr,IID_PPV_ARGS(&item)))) return nullptr;
 HBITMAP bitmap=nullptr;
 item->GetImage({size,size},static_cast<SIIGBF>(SIIGBF_ICONONLY|SIIGBF_BIGGERSIZEOK),&bitmap);
 return bitmap;
}
API BOOL LaunchTarget(const wchar_t* target,const wchar_t* args,HWND owner) {
 PIDLIST_ABSOLUTE pidl=nullptr;
 if (FAILED(SHParseDisplayName(target,nullptr,&pidl,0,nullptr))) return FALSE;
 SHELLEXECUTEINFOW s{sizeof(s)};
 s.fMask=SEE_MASK_IDLIST|SEE_MASK_FLAG_NO_UI; s.hwnd=owner;
 s.lpIDList=pidl; s.lpParameters=args; s.nShow=SW_SHOWNORMAL;
 BOOL ok=ShellExecuteEx(&s); CoTaskMemFree(pidl); return ok;
}
struct MenuState { ComPtr<IContextMenu2> two; ComPtr<IContextMenu3> three; };
static LRESULT CALLBACK MenuProc(HWND h,UINT m,WPARAM w,LPARAM l,UINT_PTR,DWORD_PTR data) {
 auto state=reinterpret_cast<MenuState*>(data);
 if (m==WM_INITMENUPOPUP||m==WM_DRAWITEM||m==WM_MEASUREITEM||m==WM_MENUCHAR) {
  LRESULT result=0;
  if (state->three && SUCCEEDED(state->three->HandleMenuMsg2(m,w,l,&result))) return result;
  if (state->two && SUCCEEDED(state->two->HandleMenuMsg(m,w,l))) return 0;
 }
 return DefSubclassProc(h,m,w,l);
}
API int ShellMenu(HWND owner,const wchar_t* target,int x,int y,BOOL queryOnly) {
 PIDLIST_ABSOLUTE pidl=nullptr;
 HRESULT hr=SHParseDisplayName(target,nullptr,&pidl,0,nullptr);
 if (FAILED(hr)) return hr;
 ComPtr<IShellFolder> parent; PCUITEMID_CHILD child=nullptr;
 hr=SHBindToParent(pidl,IID_PPV_ARGS(&parent),&child);
 ComPtr<IContextMenu> menu;
 if (SUCCEEDED(hr)) hr=parent->GetUIObjectOf(owner,1,&child,IID_IContextMenu,nullptr,reinterpret_cast<void**>(menu.GetAddressOf()));
 if (SUCCEEDED(hr)) {
  HMENU popup=CreatePopupMenu();
  if (!popup) { CoTaskMemFree(pidl); return E_OUTOFMEMORY; }
  hr=menu->QueryContextMenu(popup,0,1,0x7fff,CMF_NORMAL|((GetKeyState(VK_SHIFT)<0)?CMF_EXTENDEDVERBS:0));
  if (SUCCEEDED(hr)) {
   int count=GetMenuItemCount(popup);
   if (!queryOnly) {
    MenuState state; menu.As(&state.two); menu.As(&state.three);
    if (!SetWindowSubclass(owner,MenuProc,1,reinterpret_cast<DWORD_PTR>(&state))) { DestroyMenu(popup); CoTaskMemFree(pidl); return E_FAIL; }
    SetForegroundWindow(owner);
    UINT id=TrackPopupMenuEx(popup,TPM_RETURNCMD|TPM_RIGHTBUTTON,x,y,owner,nullptr);
    if (id) {
     CMINVOKECOMMANDINFOEX invoke{sizeof(invoke)};
     invoke.fMask=CMIC_MASK_UNICODE|CMIC_MASK_PTINVOKE;
     if (GetKeyState(VK_SHIFT)<0) invoke.fMask|=CMIC_MASK_SHIFT_DOWN;
     if (GetKeyState(VK_CONTROL)<0) invoke.fMask|=CMIC_MASK_CONTROL_DOWN;
     invoke.hwnd=owner; invoke.lpVerb=MAKEINTRESOURCEA(id-1); invoke.lpVerbW=MAKEINTRESOURCEW(id-1);
     invoke.nShow=SW_SHOWNORMAL; invoke.ptInvoke={x,y};
     hr=menu->InvokeCommand(reinterpret_cast<CMINVOKECOMMANDINFO*>(&invoke));
    }
    RemoveWindowSubclass(owner,MenuProc,1);
    PostMessage(owner,WM_NULL,0,0);
   }
   if (SUCCEEDED(hr)) hr=count;
  }
  DestroyMenu(popup);
 }
 CoTaskMemFree(pidl); return hr;
}


// System render loopback. All COM objects live and are released on the caller's
// dedicated audio worker thread. No capture endpoint (microphone) is opened.
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <audiopolicy.h>
#include <endpointvolume.h>
#include <ksmedia.h>
#include <cmath>
#include <complex>
#include <array>
struct MusicCapture {
 ComPtr<IMMDeviceEnumerator> enumerator;ComPtr<IMMDevice> device;
 ComPtr<IAudioEndpointVolume> volume;
 ComPtr<IAudioSessionManager2> sessions;
 ComPtr<IAudioClient> client;ComPtr<IAudioCaptureClient> capture;
 WAVEFORMATEX* format=nullptr;PWSTR id=nullptr;bool initialized=false;int checks=0;
 double low=0,mid=0;
 std::array<double,2048> samples{},samplesRight{};unsigned cursor=0;ULONGLONG lastSample=0;
 ~MusicCapture(){if(client)client->Stop();capture.Reset();client.Reset();volume.Reset();device.Reset();enumerator.Reset();CoTaskMemFree(format);CoTaskMemFree(id);if(initialized)CoUninitialize();}
};
API int MusicOpen(void** output){
 *output=nullptr;auto state=new MusicCapture();HRESULT hr=CoInitializeEx(nullptr,COINIT_MULTITHREADED);
 if(FAILED(hr)){delete state;return hr;}state->initialized=true;
 hr=CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&state->enumerator));
 if(SUCCEEDED(hr))hr=state->enumerator->GetDefaultAudioEndpoint(eRender,eMultimedia,&state->device);
 if(SUCCEEDED(hr))hr=state->device->GetId(&state->id);
 if(SUCCEEDED(hr))hr=state->device->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,reinterpret_cast<void**>(state->client.GetAddressOf()));
 if(SUCCEEDED(hr))state->device->Activate(__uuidof(IAudioEndpointVolume),CLSCTX_ALL,nullptr,reinterpret_cast<void**>(state->volume.GetAddressOf()));
 if(SUCCEEDED(hr))state->device->Activate(__uuidof(IAudioSessionManager2),CLSCTX_ALL,nullptr,reinterpret_cast<void**>(state->sessions.GetAddressOf()));
 if(SUCCEEDED(hr))hr=state->client->GetMixFormat(&state->format);
 if(SUCCEEDED(hr)){
  auto f=state->format;bool floating=f->wFormatTag==WAVE_FORMAT_IEEE_FLOAT;
  bool pcm=f->wFormatTag==WAVE_FORMAT_PCM;
  if(f->wFormatTag==WAVE_FORMAT_EXTENSIBLE&&f->cbSize>=22){auto ex=reinterpret_cast<WAVEFORMATEXTENSIBLE*>(f);floating=IsEqualGUID(ex->SubFormat,KSDATAFORMAT_SUBTYPE_IEEE_FLOAT);pcm=IsEqualGUID(ex->SubFormat,KSDATAFORMAT_SUBTYPE_PCM);}
  if(!((floating&&f->wBitsPerSample==32)||(pcm&&(f->wBitsPerSample==16||f->wBitsPerSample==24||f->wBitsPerSample==32))))hr=AUDCLNT_E_UNSUPPORTED_FORMAT;
 }
 if(SUCCEEDED(hr))hr=state->client->Initialize(AUDCLNT_SHAREMODE_SHARED,AUDCLNT_STREAMFLAGS_LOOPBACK,1000000,0,state->format,nullptr);
 if(SUCCEEDED(hr))hr=state->client->GetService(IID_PPV_ARGS(&state->capture));
 if(SUCCEEDED(hr))hr=state->client->Start();
 if(FAILED(hr)){delete state;return hr;}*output=state;return S_OK;
}
API void MusicClose(void* handle){delete static_cast<MusicCapture*>(handle);}
// Peak levels are read per application session, so Windows' dedicated system
// sounds session cannot displace a paused song's cover and title.
API int MusicHasOtherAudio(void* handle){
 auto s=static_cast<MusicCapture*>(handle);
 if(!s||!s->sessions)return E_NOINTERFACE;
 if(s->volume){BOOL muted=FALSE;float volume=1;
  if(SUCCEEDED(s->volume->GetMute(&muted))&&muted)return 0;
  if(SUCCEEDED(s->volume->GetMasterVolumeLevelScalar(&volume))&&volume<.001f)return 0;
 }
 ComPtr<IAudioSessionEnumerator> enumerator;
 HRESULT hr=s->sessions->GetSessionEnumerator(&enumerator);if(FAILED(hr))return hr;
 int count=0;hr=enumerator->GetCount(&count);if(FAILED(hr))return hr;
 for(int i=0;i<count;i++){
  ComPtr<IAudioSessionControl> control;
  if(FAILED(enumerator->GetSession(i,&control)))continue;
  AudioSessionState state=AudioSessionStateInactive;
  if(FAILED(control->GetState(&state))||state!=AudioSessionStateActive)continue;
  ComPtr<IAudioSessionControl2> details;
  if(SUCCEEDED(control.As(&details))){
   if(details->IsSystemSoundsSession()==S_OK)continue;
   DWORD processId=0;
   if(SUCCEEDED(details->GetProcessId(&processId))&&processId==GetCurrentProcessId())continue;
  }
  ComPtr<ISimpleAudioVolume> sessionVolume;
  if(SUCCEEDED(control.As(&sessionVolume))){BOOL muted=FALSE;float volume=1;
   if(SUCCEEDED(sessionVolume->GetMute(&muted))&&muted)continue;
   if(SUCCEEDED(sessionVolume->GetMasterVolume(&volume))&&volume<.001f)continue;
  }
  ComPtr<IAudioMeterInformation> meter;
  float peak=0;
  if(SUCCEEDED(control.As(&meter))&&SUCCEEDED(meter->GetPeakValue(&peak))&&peak>.0008f)return 1;
 }
 return 0;
}
API int MusicRead(void* handle,float* bands){
 auto s=static_cast<MusicCapture*>(handle);bands[0]=bands[1]=bands[2]=0;
 if(++s->checks%40==0){ComPtr<IMMDevice> current;PWSTR id=nullptr;HRESULT hr=s->enumerator->GetDefaultAudioEndpoint(eRender,eMultimedia,&current);if(FAILED(hr))return hr;hr=current->GetId(&id);bool changed=FAILED(hr)||!id||wcscmp(id,s->id)!=0;CoTaskMemFree(id);if(changed)return AUDCLNT_E_DEVICE_INVALIDATED;}
 auto f=s->format;bool floating=f->wFormatTag==WAVE_FORMAT_IEEE_FLOAT;
 if(f->wFormatTag==WAVE_FORMAT_EXTENSIBLE)floating=IsEqualGUID(reinterpret_cast<WAVEFORMATEXTENSIBLE*>(f)->SubFormat,KSDATAFORMAT_SUBTYPE_IEEE_FLOAT);
 double energy[3]{},a=1-std::exp(-2*3.141592653589793*180/f->nSamplesPerSec),b=1-std::exp(-2*3.141592653589793*2200/f->nSamplesPerSec);UINT total=0,packet=0;
 HRESULT hr=s->capture->GetNextPacketSize(&packet);
 while(SUCCEEDED(hr)&&packet){BYTE* data=nullptr;UINT frames=0;DWORD flags=0;hr=s->capture->GetBuffer(&data,&frames,&flags,nullptr,nullptr);if(FAILED(hr))break;
  for(UINT i=0;i<frames;i++){
   double sample=0,leftSample=0,rightSample=0;if(!(flags&AUDCLNT_BUFFERFLAGS_SILENT)){
    // Select the strongest channel to avoid cancellation of anti-phase stereo.
    for(UINT ch=0;ch<f->nChannels;ch++){BYTE* p=data+i*f->nBlockAlign+ch*(f->wBitsPerSample/8);double v=0;
     if(floating){float v32;memcpy(&v32,p,4);v=std::isfinite(v32)?v32:0;}
     else if(f->wBitsPerSample==16){short v16;memcpy(&v16,p,2);v=v16/32768.0;}
     else if(f->wBitsPerSample==24){int v24=(p[0]|p[1]<<8|p[2]<<16);if(v24&0x800000)v24|=~0xffffff;v=v24/8388608.0;}
     else {int v32;memcpy(&v32,p,4);v=v32/2147483648.0;}
     if(ch==0)leftSample=v;if(ch==1)rightSample=v;
     if(std::abs(v)>std::abs(sample))sample=v;
    }
   }
   s->samples[s->cursor%2048]=leftSample;s->samplesRight[s->cursor%2048]=f->nChannels>1?rightSample:leftSample;s->cursor++;s->lastSample=GetTickCount64();
   s->low+=a*(sample-s->low);s->mid+=b*(sample-s->mid);double v[3]={s->low,s->mid-s->low,sample-s->mid};for(int k=0;k<3;k++)energy[k]+=v[k]*v[k];total++;
  }
  hr=s->capture->ReleaseBuffer(frames);if(FAILED(hr))break;hr=s->capture->GetNextPacketSize(&packet);
 }
 if(total)for(int k=0;k<3;k++)bands[k]=static_cast<float>(std::sqrt(energy[k]/total));
 return hr;
}

static void Spectrum(const double* samples,unsigned cursor,unsigned rate,float* bands){
 constexpr unsigned N=2048;std::array<std::complex<double>,N> bins;
 static const auto hann=[](){std::array<double,N> values{};for(unsigned i=0;i<N;i++)values[i]=.5-.5*std::cos(2*3.141592653589793*i/(N-1));return values;}();
 for(unsigned i=0;i<N;i++)bins[i]=samples[(cursor+i)%N]*hann[i];
 for(unsigned i=1,j=0;i<N;i++){unsigned bit=N>>1;for(;j&bit;bit>>=1)j^=bit;j^=bit;if(i<j)std::swap(bins[i],bins[j]);}
 for(unsigned length=2;length<=N;length<<=1){auto wlen=std::polar(1.0,-2*3.141592653589793/length);for(unsigned i=0;i<N;i+=length){std::complex<double> w=1;for(unsigned j=0;j<length/2;j++){auto u=bins[i+j],v=bins[i+j+length/2]*w;bins[i+j]=u+v;bins[i+j+length/2]=u-v;w*=wlen;}}}
 // Crop the final quarter of the former logarithmic display range, then
 // redistribute all 48 bands across the retained range.
 double upper=60*std::pow(std::min(16000.0,rate*.45)/60,.75);
 for(int i=0;i<48;i++){double lo=60*std::pow(upper/60,i/48.0),hi=60*std::pow(upper/60,(i+1)/48.0),energy=0;
  unsigned start=std::max(1u,static_cast<unsigned>(std::ceil(lo*N/rate))),end=std::min(N/2,static_cast<unsigned>(std::ceil(hi*N/rate)));
  if(end<=start)end=start+1;
  for(unsigned k=start;k<end;k++)energy=std::max(energy,std::norm(bins[k]));bands[i]=static_cast<float>(std::sqrt(energy)*4/N);
 }
}
API int MusicReadSpectrum(void* handle,float* bands){float coarse[3];int hr=MusicRead(handle,coarse);if(FAILED(hr))return hr;auto s=static_cast<MusicCapture*>(handle);if(GetTickCount64()-s->lastSample>45){for(int i=0;i<48;i++)bands[i]=0;return S_OK;}Spectrum(s->samples.data(),s->cursor,s->format->nSamplesPerSec,bands);if(s->format->nChannels>1){float right[48];Spectrum(s->samplesRight.data(),s->cursor,s->format->nSamplesPerSec,right);for(int i=0;i<48;i++)bands[i]=std::sqrt((bands[i]*bands[i]+right[i]*right[i])*.5f);}float gain=1;BOOL muted=FALSE;if(s->volume){s->volume->GetMasterVolumeLevelScalar(&gain);s->volume->GetMute(&muted);}for(int i=0;i<48;i++)bands[i]*=muted?0:gain;return S_OK;}
API void MusicTestSpectrum(double frequency,float* bands){std::array<double,2048> data;for(unsigned i=0;i<data.size();i++)data[i]=.1*std::sin(2*3.141592653589793*frequency*i/48000);Spectrum(data.data(),0,48000,bands);}





