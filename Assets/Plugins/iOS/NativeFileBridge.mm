#import <UIKit/UIKit.h>
#import <Foundation/Foundation.h>

#if __has_include(<UniformTypeIdentifiers/UniformTypeIdentifiers.h>)
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#define HABIT_HAS_UTTYPE 1
#endif

// Sistem dosya secicisinin iOS yarisi. C# tarafi: NativeFileBridge.cs.
//
// UIDocumentPickerViewController hicbir Info.plist anahtari gerektirmiyor
// (UIFileSharingEnabled / LSSupportsOpeningDocumentsInPlace GEREKMEZ) --
// kullanici kendi dosyasini sectigi icin izin modeli zaten karsilanmis
// sayiliyor.
//
// Secme modu asCopy:YES acilir (sistem dosyayi uygulamanin Inbox klasorune
// kopyalar); yine de okuma oncesi security-scoped erisim acilir/kapatilir.
// BU DOSYA JSON TASIMAZ, DOSYA KOPYALAR: kaydetmede C#'in verdigi yerel
// dosyayi kullanicinin sectigi yere, secmede secilen dosyayi C#'in verdigi
// yerel yola.
//
// Hata metinleri KISA ASCII JETONLARI ("write"/"read"/"nopicker") --
// kullaniciya gorunen Turkce cumleler C# tarafinda (NativeFileBridge.
// TranslateError).
//
// DIKKAT: bu dosya Windows'ta DERLENMIYOR. Obj-C'nin ilk gercek derlemesi bir
// Mac/Xcode (ya da bulut macOS CI) uzerinde olacak; orada bir-iki import/
// sozdizimi duzeltmesi gerekebilir. Mimari surpriz beklenmiyor.

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

// C# tarafindaki NativeFileBridge.ReceiverObjectName / ResultMethodName ile
// BIREBIR ayni olmak zorunda: UnitySendMessage nesneyi ADINA gore buluyor.
static NSString* const kReceiverObject = @"NativeFileBridge";
static NSString* const kReceiverMethod = @"OnNativeFileResult";

// Payload: "<outcome>|<displayName>|<error>|<path>" -- yol EN SONDA, cunku C#
// tarafi alan sayisi vererek boluyor ve son alan ayracli da olsa ham kaliyor.
static void HabitSendResult(NSString* outcome, NSString* displayName, NSString* error, NSString* path)
{
    NSString* safeName = displayName == nil ? @"" : [displayName stringByReplacingOccurrencesOfString:@"|" withString:@" "];
    NSString* safeError = error == nil ? @"" : [error stringByReplacingOccurrencesOfString:@"|" withString:@" "];
    NSString* safePath = path == nil ? @"" : path;

    NSString* payload = [NSString stringWithFormat:@"%@|%@|%@|%@", outcome, safeName, safeError, safePath];

    UnitySendMessage([kReceiverObject UTF8String], [kReceiverMethod UTF8String], [payload UTF8String]);
}

static UIViewController* HabitRootViewController(void)
{
    UIWindow* window = nil;

    // Unity'nin kendi penceresi; bulunamazsa etkin sahneden ilerliyoruz.
    NSArray<UIWindow*>* windows = [UIApplication sharedApplication].windows;
    for (UIWindow* candidate in windows)
    {
        if (candidate.isKeyWindow)
        {
            window = candidate;
            break;
        }
    }

    if (window == nil && windows.count > 0)
    {
        window = windows[0];
    }

    UIViewController* controller = window.rootViewController;

    // Zaten bir sey sunuluyorsa (ornegin baska bir modal) en ustteki
    // uzerinden sunmak zorundayiz.
    while (controller.presentedViewController != nil)
    {
        controller = controller.presentedViewController;
    }

    return controller;
}

// Secicinin delegate'i. Tek bekleyen islem oldugu icin (C# tarafi bunu
// garanti ediyor) tek bir paylasilan ornek yeterli; ornek, secici kapanana
// kadar KENDINI TUTUYOR (gSharedDelegate), yoksa ARC onu serbest birakir ve
// geri cagri hic gelmez.
@interface HabitFilePickerDelegate : NSObject <UIDocumentPickerDelegate>
@property(nonatomic, copy) NSString* localPath;
@property(nonatomic, copy) NSString* suggestedName;
@property(nonatomic, assign) BOOL isSave;
@end

static HabitFilePickerDelegate* gSharedDelegate = nil;

@implementation HabitFilePickerDelegate

- (void)finishWithOutcome:(NSString*)outcome
              displayName:(NSString*)displayName
                    error:(NSString*)error
                     path:(NSString*)path
{
    HabitSendResult(outcome, displayName, error, path);

    // Bu, delegate'in son referansi -- buradan sonra serbest kalabilir.
    if (gSharedDelegate == self)
    {
        gSharedDelegate = nil;
    }
}

- (void)documentPicker:(UIDocumentPickerViewController*)controller
didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    NSURL* url = urls.firstObject;

    if (url == nil)
    {
        [self finishWithOutcome:@"cancel" displayName:@"" error:@"" path:@""];
        return;
    }

    if (self.isSave)
    {
        // Kaydetme modunda sistem kopyalamayi KENDISI yapti; bize yalnizca
        // nereye gittigini bildiriyor.
        [self finishWithOutcome:@"ok"
                    displayName:[url lastPathComponent]
                          error:@""
                           path:[url path]];
        return;
    }

    // Secme modunda icerigi C#'in verdigi yerel yola kopyaliyoruz. Dosya
    // uygulama kutusunun DISINDA olabilir; kopya modunda (asCopy) NO doner ve
    // zararsizdir, ama "yerinde ac" modunda bu cagri olmadan okuma izin
    // hatasiyla duser -- savunma olarak her durumda cagiriyoruz.
    BOOL scoped = [url startAccessingSecurityScopedResource];

    NSError* coordinateError = nil;
    __block BOOL copied = NO;
    __block NSString* pickedName = [url lastPathComponent];

    NSFileCoordinator* coordinator = [[NSFileCoordinator alloc] initWithFilePresenter:nil];
    [coordinator coordinateReadingItemAtURL:url
                                   options:NSFileCoordinatorReadingWithoutChanges
                                     error:&coordinateError
                                byAccessor:^(NSURL* readable)
    {
        // copyItemAtPath yerine veri kopyasi: bazi dosya saglayicilarinda
        // oznitelik kopyalamasi dusuyor; atomik yazim eski dosyayi silme
        // ihtiyacini da ortadan kaldiriyor. Dosya kucuk (40-80 KB).
        NSError* readError = nil;
        NSData* data = [NSData dataWithContentsOfURL:readable options:0 error:&readError];

        if (data == nil)
        {
            NSLog(@"NativeFileBridge: okuma basarisiz %@", readError);
            return;
        }

        NSError* writeError = nil;
        copied = [data writeToFile:self.localPath options:NSDataWritingAtomic error:&writeError];

        if (!copied)
        {
            NSLog(@"NativeFileBridge: yazma basarisiz %@", writeError);
        }
    }];

    if (scoped)
    {
        [url stopAccessingSecurityScopedResource];
    }

    if (!copied)
    {
        NSLog(@"NativeFileBridge: koordineli okuma sonucu %@", coordinateError);
        [self finishWithOutcome:@"error" displayName:@"" error:@"read" path:@""];
        return;
    }

    [self finishWithOutcome:@"ok" displayName:pickedName error:@"" path:self.localPath];
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    [self finishWithOutcome:@"cancel" displayName:@"" error:@"" path:@""];
}

@end

static void HabitPresent(UIDocumentPickerViewController* picker, HabitFilePickerDelegate* delegate)
{
    UIViewController* root = HabitRootViewController();

    if (root == nil)
    {
        gSharedDelegate = nil;
        HabitSendResult(@"error", @"", @"nopicker", @"");
        return;
    }

    picker.delegate = delegate;
    picker.modalPresentationStyle = UIModalPresentationFormSheet;

    [root presentViewController:picker animated:YES completion:nil];
}

extern "C" void HabitNativeSaveFile(const char* sourcePath, const char* fileName)
{
    NSString* source = sourcePath == NULL ? @"" : [NSString stringWithUTF8String:sourcePath];
    NSString* name = fileName == NULL ? @"" : [NSString stringWithUTF8String:fileName];

    if (source.length == 0 || ![[NSFileManager defaultManager] fileExistsAtPath:source])
    {
        HabitSendResult(@"error", @"", @"write", @"");
        return;
    }

    // Secicide gorunen ad DOSYA ADI oldugu icin, onerilen adla gecici bir
    // kopya uretiyoruz -- kaynak dosyanin adi (tarih damgali kurtarma kopyasi)
    // kullaniciya gosterilmek icin degil.
    NSString* staged = [NSTemporaryDirectory() stringByAppendingPathComponent:
        name.length == 0 ? [source lastPathComponent] : name];

    NSFileManager* manager = [NSFileManager defaultManager];

    if ([manager fileExistsAtPath:staged])
    {
        [manager removeItemAtPath:staged error:nil];
    }

    NSError* stageError = nil;
    if (![manager copyItemAtPath:source toPath:staged error:&stageError])
    {
        NSLog(@"NativeFileBridge: gecici kopya uretilemedi %@", stageError);
        HabitSendResult(@"error", @"", @"write", @"");
        return;
    }

    HabitFilePickerDelegate* delegate = [[HabitFilePickerDelegate alloc] init];
    delegate.localPath = staged;
    delegate.suggestedName = name;
    delegate.isSave = YES;
    gSharedDelegate = delegate;

    NSURL* url = [NSURL fileURLWithPath:staged];
    UIDocumentPickerViewController* picker = nil;

    if (@available(iOS 14.0, *))
    {
        picker = [[UIDocumentPickerViewController alloc] initForExportingURLs:@[url] asCopy:YES];
    }
    else
    {
        picker = [[UIDocumentPickerViewController alloc] initWithURL:url
                                                             inMode:UIDocumentPickerModeExportToService];
    }

    HabitPresent(picker, delegate);
}

extern "C" void HabitNativePickFile(const char* destPath)
{
    NSString* dest = destPath == NULL ? @"" : [NSString stringWithUTF8String:destPath];

    if (dest.length == 0)
    {
        HabitSendResult(@"error", @"", @"read", @"");
        return;
    }

    HabitFilePickerDelegate* delegate = [[HabitFilePickerDelegate alloc] init];
    delegate.localPath = dest;
    delegate.suggestedName = @"";
    delegate.isSave = NO;
    gSharedDelegate = delegate;

    UIDocumentPickerViewController* picker = nil;

#ifdef HABIT_HAS_UTTYPE
    if (@available(iOS 14.0, *))
    {
        // JSON'un yaninda duz metin de: bazi kaynaklar .json dosyasini
        // public.text olarak isaretliyor ve yalnizca UTTypeJSON ile
        // secilemez hale geliyor.
        // asCopy:YES -- sistem dosyayi uygulamanin kendi Inbox klasorune
        // kopyalayip o URL'i veriyor; security-scoped erisim sorunu kalmiyor
        // (disa aktarmadaki initForExportingURLs:asCopy:YES ile simetrik).
        picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[UTTypeJSON, UTTypeText] asCopy:YES];
    }
#endif

    if (picker == nil)
    {
        picker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:@[@"public.json", @"public.text"]
                                                                       inMode:UIDocumentPickerModeImport];
    }

    HabitPresent(picker, delegate);
}
