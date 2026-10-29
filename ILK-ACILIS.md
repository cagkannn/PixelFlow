# PixelFlow’u Unity 6.6’da aç

Hazır projeyi açacağız. Önceki oyundaki altı bölüm, UFO sıraları ve atış hakları burada bulunuyor. İnternet üzerinden çalışan eski oyun ayrı duruyor; yeni çalışmalarımızı Unity klasöründe sürdüreceğiz.

## İlk kez açarken

1. [GitHub proje sayfasını](https://github.com/cagkannn/PixelFlow) aç. Yeşil **Code** düğmesine, ardından **Download ZIP** seçeneğine bas. İnen **PixelFlow-main.zip** dosyasına sağ tıklayıp **Tümünü ayıkla / Extract All** seç. Projeyi sıkıştırılmış dosyanın içinden açma.
2. **Unity Hub**’ı aç ve soldan **Projects** bölümüne gir. Bu, projelerinin listelendiği yer.
3. **Add → Add project from disk** seç. Bazı Hub sürümlerinde **Add** düğmesinin yanındaki küçük oka basmak gerekiyor.
4. Açılan pencerede çıkardığın **PixelFlow-main** klasörünün içindeki **PixelFlow-Unity** klasörünü seç. Doğru klasörün içinde **Assets**, **Packages** ve **ProjectSettings** birlikte bulunur. **Assets** klasörünün içine girme.
5. Unity sürümü sorulursa bilgisayarındaki **Unity 6.6** kurulumunu seç. Küçük sürüm numarası farklıysa, kurulu 6.6 sürümünle açabilirsin.
6. Projeye çift tıkla. İlk açılışta Unity gerekli dosyaları hazırlayacak. Bu işlem bittikten sonra oyun alanı otomatik oluşturulur. Oluşmazsa üst menüden **PixelFlow → Oyun sahnesini hazırla** seç.
7. Ortadaki **Game** sekmesine geç. Burada oyuncunun gördüğü ekranı görürsün. En üstteki **▶ Play** üçgenine bas. Alternatif olarak **PixelFlow → Oyunu çalıştır** menüsünü kullan.
8. Alttaki UFO’lardan birini seç. Kutudaki uçak tekerleklerine binip yola çıkmalı. Aynı renkteki karelere ateş eder. Sayısı 0 olduğunda hemen kaybolur.

Unity’de iki görünüm kullanacağız: **Scene**, oyundaki parçaları yerleştirdiğin çalışma alanıdır. **Game**, kameranın oyuncuya gösterdiği ekrandır. Oyun oynanırken Game sekmesinde ol.

## İlk denemede kontrol et

- Kutunun içi, kalın duvarları ve lastiklerin yan yüzleri görünüyor mu?
- UFO üzerine tıkladığında tekerleklere atlayıp ilerliyor mu?
- Mor UFO mor, sarı UFO sarı kareleri vuruyor mu?
- Sayı 0 olunca UFO kayboluyor ve yeni UFO gönderebiliyor musun?
- Aynı anda yolda en fazla beş UFO bulunuyor mu?
- Alttaki pervaneyi seçip bir UFO’ya bastığında ortadan havadan atış yapıyor mu?

Üstteki **Bölüm** düğmesiyle önceki veya sonraki bölüme geçebilirsin. Duraklatma ve yeniden başlatma simgeleri yanındadır. Toplam beş UFO kaldığında hız kendiliğinden ikiye çıkar; yeni bölümde normale döner.

## Kendi bölümünü yap

1. Oyun açıksa en üstteki **▶ Play** düğmesine tekrar basıp durdur.
2. **PixelFlow → Bölüm tasarla** menüsünü aç. Bu, birlikte bölüm yapacağımız çizim alanıdır.
3. Hazır bölümlerden birini seçip kopyası üzerinde çalışabilir veya boş bölüm açabilirsin.
4. Bir renk seç; karelere basarak boya. Basılı tutup sürükleyebilirsin. Sağ tık kareyi siler.
5. UFO listesinden rengi, atış hakkını ve hangi sütunda bekleyeceğini ayarla. İstersen **Karelere yetecek UFO’ları hazırla** düğmesini kullan: her rengin toplam atış hakkı o rengin kare sayısına eşit olur.
6. **Kaydet ve deneme sahnesini aç** düğmesine bas. Sonra **Game → ▶ Play** ile kendi bölümünü oyna.

Hazır altı bölümün üstüne yazılmaz. Tasarımların **Assets/Resources/Levels/custom-levels.json** dosyasına kaydedilir. Bu dosyayı yedeklersen bölüm çalışmalarını da saklamış olursun. **Dosyaya kaydet** ile ayrıca dosya alabilirsin; JSON, bölümün renk ve sayı bilgilerini saklayan yazı dosyasıdır.

Play açıkken Unity’de yaptığın geçici değişiklikler durdurunca kaybolabilir. Kalıcı bölüm değişiklikleri için çizim penceresinde **Kaydet** kullan.

## Bir şey çalışmazsa

- **PixelFlow** menüsü görünmüyorsa Unity hâlâ dosyaları hazırlıyor olabilir. Sağ alttaki işlemin bitmesini bekle.
- **Window → General → Console** bölümünde kırmızı bir hata varsa, ilk kırmızı mesajın tamamını bana gönder. Bu, Unity’nin neyi çalıştıramadığını söyler.
- Oyun görünmüyorsa **PixelFlow → Oyun sahnesini hazırla**, ardından **Game → ▶ Play** yap.
- Görüntü pembe görünürse projeyi bu paketteki ayarlarla açtığını kontrol et. Bu proje **Built-In** görüntüleme düzenini kullanır; başka projenin içine dosya taşımadan, kendi klasörü olarak aç.
- Klavye veya fareyle ilgili Input hatası görürsen **Edit → Project Settings → Player → Other Settings → Active Input Handling** alanını **Input Manager (Old)** seçip Unity’yi yeniden aç. Projede bu ayar zaten kayıtlıdır.

## Bu pakette ne kontrol edildi?

Oyun kuralları C# ile derlenip çalıştırıldı; altı bölüm verilen araçlarla tamamlandı. Unity kodları, gerçek Unity başvuru dosyalarıyla derleme kontrolünden geçirildi. Bu bulut ortamında Unity Editörü bulunmadığı için **Unity 6.6 içinde Play ve görüntü kontrolü henüz yapılmadı**. İlk açılışta bunu bilgisayarında birlikte kontrol edeceğiz.
