# PixelFlow — Unity 6.6

Unity Hub’a bu klasörü ekle: içinde **Assets**, **Packages** ve **ProjectSettings** bulunur. İlk açılışta **PixelFlow** menüsü oyun sahnesini hazırlar. **Game** sekmesine geçip **▶ Play** ile oyna.

Adım adım açılış ve bölüm çizme kılavuzu paketin üstündeki **ILK-ACILIS.md** dosyasındadır.

## Çalışma alanları

- **PixelFlow → Oyun sahnesini hazırla:** Üç boyutlu kutu, yol, küpler, uçak iniş takımları ve UFO sırasını oluşturur. Üretilen parçalar normal Unity dosyaları olarak kaydedilir; projeyi kapatıp açınca görünmeye devam eder.
- **PixelFlow → Oyunu çalıştır:** Hazır oyun sahnesini açıp başlatır.
- **PixelFlow → Bölüm tasarla:** Kareleri boya, UFO sırasını ve atış haklarını ayarla, ayrı bölüm olarak kaydet ve dene.
- **PixelFlow → Bilgisayar için oyun çıkar:** Unity kurulumundaki desteğe uygun çalıştırılabilir oyun dosyası hazırlar.

## Dosyalar

| Yer | Ne işe yarar? |
| --- | --- |
| Assets/Scripts/Core | Atış, tur, sıra, beş UFO sınırı ve bölüm bitişi kuralları. Unity görüntüsünden ayrı çalışır. |
| Assets/Scripts/Runtime | Kuralları ekrandaki hareket, tıklama ve atışlara bağlar. |
| Assets/Scripts/Presentation | Gerçek üç boyutlu UFO, kutu, lastik ve küpleri oluşturur. |
| Assets/Scripts/UI | Bölüm, duraklatma, yeniden başlatma ve pervane düğmeleri. |
| Assets/Editor | Sahneyi hazırlama ve bölüm çizme araçları. |
| Assets/Resources/Levels/reference-levels.json | Videodan yeniden kurulan altı bölüm. Özgün oyunun dışa aktarılmış dosyaları değildir. |
| Assets/Resources/Levels/custom-levels.json | Kendi kaydettiğin bölümler. İlk kayıttan sonra oluşur. |
| Assets/Generated | İlk hazırlamada kaydedilen üç boyutlu şekiller ve yüzeyler. |
| Assets/Scenes/PixelFlow.unity | İlk hazırlamada kaydedilen oyun sahnesi. |

Başlangıç bölümleri sırasıyla 380, 320, 240, 397, 520 ve 328 dolu kare içerir. Her rengin toplam atış hakkı, kare sayısına eşittir. Sayısı 0 olan UFO ve iniş takımı son atışta hemen kaldırılır; son top hedefe gider. Havadan atış bir kez kullanılabilir. Aynı anda en fazla beş UFO bulunur; dönen bir UFO’nun inişi bitene kadar yeri dolu sayılır. Son beş UFO’da oyun iki kat hızlanır.

Bu proje Built-In görüntüleme düzenini, gerçek hacimli parçaları, ışığı ve gölgeleri kullanır. Çalışmak için Unity Asset Store’dan ürün alman gerekmez. İlk açılışta Unity kendi ücretsiz arayüz paketini hazırlayabilir.

Unity 6.6 Editörü bu bulut ortamında çalıştırılmadı. C# oyun kuralları gerçekten derlenip çalıştırıldı; başvuru dosyalarıyla kod uyumluluğu ve şekillerin ölçüleri ayrıca kontrol edildi. İlk yerel Play denemesi açılış kılavuzundadır.
