using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Şehir verilerini tuttuğum sınıf. Inspector'da görünmesi için Serializable yaptım.
[System.Serializable]
public class CityLevel
{
    public string cityName;          // Şehrin adı
    public Sprite cityBackground;    // Arka plan resmi
    public string[] wordList;        // O seviyeye özel kelimeler
    public int timeLimit;            // Bombanın patlama süresi
    public int maxTeamSize;          // Kaç tane dedektif hakkım var?
    public Color cityColor;          // Şehre özel tema rengi
    [HideInInspector] public bool isUnlocked = false; // Kilitli mi? (Bunu koddan yönetiyorum)
    public string wordFileName;      // Resources klasöründeki kelime dosyasının adı
}

public class DetectiveBombGame : MonoBehaviour
{
    // ========================================================================
    //                  --- DEĞİŞKENLER VE TANIMLAMALAR ---
    // ========================================================================

    [Header("UI Elemanları")]
    public Text passwordDisplayText; // Ekranda "_ _ A _" şeklinde görünen kısım
    public Text teamCountText;       // Kalan canımı gösteren yazı
    public Text messageText;         // "Kazandın/Kaybettin" mesajları
    public Text levelText;           // Hangi şehirde olduğumuzu yazar
    public Text bombTimerText;       // Geri sayım sayacı

    [Header("Buton Kontrolleri")]
    public Button newGameButton;
    public Button nextLevelButton;
    public Button previousLevelButton;
    public Transform keyboardParent;    // Harf butonlarını içine dizeceğim kutu
    public GameObject keyButtonPrefab;  // Kodla oluşturacağım harf butonu taslağı

    [Header("Görseller")]
    public Image[] teamMemberImages; // Ekip arkadaşlarımın resimleri (canlarım)
    public Image cityBackground;     // Arka plan görseli

    [Header("Ses ve Efektler")]
    public AudioSource correctSound;    // Doğru bilince çalan ses
    public AudioSource wrongSound;      // Yanlış bilince çalan ses
    public AudioSource explosionSound;  // Bomba sesiu!
    public AudioSource victorySound;    // Zafer müziği
    public ParticleSystem explosionEffect; // Patlama efekti
    public ParticleSystem confettiEffect;  // Konfeti efekti

    [Header("Level Yönetimi")]
    public CityLevel[] cityLevels;      // Tüm bölümleri burada tutuyorum
    public int currentLevelIndex = 0;   // Şu an kaçıncı bölümdeyim?

    [Header("Yedek Ayarlar")]
    // Eğer dosya yüklenemezse oyun çökmesin diye bu kelimeleri kullanırım default olarak geliyor ynai
    public string[] defaultPasswordList = { "BAKIRÇAY", "FATİH", "BAYAZIT", "EFE", "BİLGİSAYAR" };

    // Oyunun iç işleyişi için gereken gizli değişkenlerim
    private string currentPassword;       // O anki gizli kelime
    private HashSet<char> guessedLetters; // Hangi harflere bastığımı burada tutuyorum
    private int teamMembersLeft;          // Kalan hakkım
    private int maxTeamSize;              // Maksimum hakkım
    private float bombTimerDuration;      // Süre sınırı
    private bool gameOver;                // Oyun bitti mi?
    private List<Button> keyboardButtons = new List<Button>(); // Oluşturduğum tuşları listede tutuyorum
    private float bombTimer;              // Geri sayım sayacı
    private bool timerRunning;            // Süre işliyor mu?

    // ========================================================================
    // --- UNITY TEMEL METOTLARI ---
    // ========================================================================

    void Awake()
    {
        // Oyun açılmadan önce kayıtlı seviyeleri yüklüyorum
        LoadLevelProgress();
    }

    void Start()
    {
        // Klavyeyi bir kere oluşturuyorum, sonra hep aynısını kullanacağım
        CreateKeyboard();

        // Butonlara tıklayınca ne yapacaklarını söylüyorum
        newGameButton.onClick.AddListener(StartNewGame);
        nextLevelButton.onClick.AddListener(NextLevel); 
        previousLevelButton.onClick.AddListener(PreviousLevel);

        // Her şey hazırsa ilk oyunu başlatıyorum!
        StartNewGame();
    }

    void Update()
    {
        // Eğer oyun bitmediyse ve süre işliyorsa sayacı aktid edeiyorum
        if (timerRunning && !gameOver)
        {
            bombTimer -= Time.deltaTime;
            bombTimerText.text = "Kalan Süre: " + Mathf.CeilToInt(bombTimer);

            // Süre biterse oyunu kaybetmiş oluruz
            if (bombTimer <= 0) GameOver(false);
        }
    }

    // ========================================================================
    // ---                  OYUN AKIŞI (GAME FLOW) ---
    // ========================================================================

    void StartNewGame()
    {
        // O anki bölümün bilgilerini ve kelimelerini yüklüyorum
        LoadCurrentLevel();

        // Kilit Kontrolü: Eğer bu şehir henüz açılmadıysa oyunu başlatmıyorum
        if (!cityLevels[currentLevelIndex].isUnlocked)
        {
            messageText.text = "BU ŞEHİR KİLİTLİ! Önceki görevleri tamamla!";
            messageText.color = Color.yellow;
            passwordDisplayText.text = "----";
            timerRunning = false;
            // Klavyeyi de kilitliyorum ki basamasınlar
            foreach (Button btn in keyboardButtons) btn.interactable = false;
            return;
        }

        // Listeden rastgele bir kelime seçip büyük harf veya harfleree çeviriyorum
        string[] currentWordList = GetCurrentWordList();
        currentPassword = currentWordList[Random.Range(0, currentWordList.Length)].ToUpper();

        // Değişkenleri sıfırlıyorum (Yeni oyun hazırlığı)
        guessedLetters = new HashSet<char>();
        teamMembersLeft = maxTeamSize;
        gameOver = false;
        bombTimer = bombTimerDuration;
        timerRunning = true;

        // Ekranı temizleyip kullanıcıya mesaj veriyorum Başlangıç textimiz bu olaacak
        messageText.text = "Dedektif! Bombayı etkisiz hale getir!";
        messageText.color = Color.white;
        cityBackground.color = Color.white;

        // Görselleri ve tuşları yeniliyorum
        UpdateDisplay();
        ResetKeyboard();
        ResetTeamVisuals();

        // Önceki oyundan kalan efektler varsa temizliyorum
        explosionEffect.Stop(); explosionEffect.Clear();
        confettiEffect.Stop(); confettiEffect.Clear();
    }

    // Oyun bittiğinde çalışan ana fonksiyonum
    void GameOver(bool won)
    {
        gameOver = true;
        timerRunning = false; // Süreyi durdur

        if (won)
        {
            // Eğer kazandıysak ve sonraki level varsa, kilidini açıp kaydediyorum
            if (currentLevelIndex < cityLevels.Length - 1)
            {
                cityLevels[currentLevelIndex + 1].isUnlocked = true;
                PlayerPrefs.SetInt("Level_" + (currentLevelIndex + 1) + "_Unlocked", 1);
                PlayerPrefs.Save();
            }

            // Zafer kutlaması!
            messageText.text = "TEBRİKLER! Şehir Kurtuldu!";
            messageText.color = Color.green;
            victorySound.Play();
            confettiEffect.Play();
        }
        else
        {
            // Kaybetme durumu :(
            messageText.text = "BOMBA PATLADI!";
            messageText.color = Color.red;
            passwordDisplayText.text = currentPassword; // Kelimeyi gösterelim ki merak etmesinler
            explosionSound.Play();
            explosionEffect.Play();
        }

        // Oyun bittiği için klavyeyi devre dışı bırakıyorum
        foreach (Button btn in keyboardButtons) btn.interactable = false;
    }

    // QUit butonuna basınca burası çalışır
    public void QuitGame()
    {
        Debug.Log("Oyundan çıkılıyor...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Editördeysek durdur
#else
        Application.Quit(); // Normal oyundaysak kapat
#endif
    }

    // ========================================================================
    //                      --- LEVEL SİSTEMİ ---
    // ========================================================================

    public void NextLevel()
    {
        // Sonraki level var mı ve kilidi açık mı diye kontrol ediyorum
        if (currentLevelIndex < cityLevels.Length - 1)
        {
            if (!cityLevels[currentLevelIndex + 1].isUnlocked)
            {
                messageText.text = "Sonraki şehir henüz kilitli!";
                messageText.color = Color.yellow;
                return;
            }
            // Sorun yoksa bir sonraki levele geç
            currentLevelIndex++;
            StartNewGame();
        }
    }

    public void PreviousLevel()
    {
        // Geriye dönülebilecek bir level varsa dönüyorum. Yani isteğe bağlı olarak açık levellerda gezinme imkanı sağlıyorum
        if (currentLevelIndex > 0)
        {
            currentLevelIndex--;
            StartNewGame();
        }
    }

    // PlayerPrefs'ten daha önce hangi levelleri açtığımı okuyorum
    void LoadLevelProgress()
    {
        for (int i = 0; i < cityLevels.Length; i++)
        {
            // İlk level (0) her zaman açıktır  diğerlerini hafızadan kontrol et
            int unlocked = PlayerPrefs.GetInt("Level_" + i + "_Unlocked", i == 0 ? 1 : 0);
            cityLevels[i].isUnlocked = (unlocked == 1);
        }
    }

    // Seçili levelin tüm ayarlarını (resim, süre, başlık) UI'ya basıyorum
    void LoadCurrentLevel()
    {
        CityLevel level = cityLevels[currentLevelIndex];

        maxTeamSize = level.maxTeamSize;
        bombTimerDuration = level.timeLimit;

        if (level.cityBackground != null)
            cityBackground.sprite = level.cityBackground;

        string lockIcon = level.isUnlocked ? "" : " 🔒";
        levelText.text = $"{level.cityName} - Seviye {currentLevelIndex + 1}{lockIcon}";

        // O şehre ait kelime dosyasını yüklüyorum easy medium ve hard vardı bende
        LoadWordsFromFile();
    }

    // ========================================================================
    //                  --- KLAVYE VE TAHMİN MANTIĞI ---
    // ========================================================================

    void CreateKeyboard()
    {
        // Türk alfabesindeki harfleri tek tek buton olarak oluşturuyorum. İsteğe bağlı farklı harfler kullanılabilir hatta kiril alfabesi bile kullanabilirsiniz çüünkü bu  alphabet kısmında hepsini ayırıp tek tek buton oluştruyor. 
        string alphabet = "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ";
        foreach (char letter in alphabet)
        {
            GameObject btnObj = Instantiate(keyButtonPrefab, keyboardParent);
            Button btn = btnObj.GetComponent<Button>();
            btn.GetComponentInChildren<Text>().text = letter.ToString();

            // Closure sorunu olmasın diye harfi kopyalıyorum
            char letterCopy = letter;
            btn.onClick.AddListener(() => GuessLetter(letterCopy));
            keyboardButtons.Add(btn);
        }
    }

    // Bir harfe basıldığında ne olacağına burada karar veriyorum
    void GuessLetter(char letter)
    {
        // Oyun bittiyse veya harf zaten seçildiyse işlem yapma
        if (gameOver || guessedLetters.Contains(letter)) return;
        guessedLetters.Add(letter);

        if (!currentPassword.Contains(letter))
        {
            // Yanlış tahmin! Can azalt, görseli sil, sesi çal.
            teamMembersLeft--;
            RemoveTeamMember();
            wrongSound.Play();
            if (teamMembersLeft <= 0) GameOver(false);
        }
        else
        {
            // Doğru tahmin!
            correctSound.Play();
            UpdateDisplay();
            // Kelime tamamlandı mı kontrol et
            if (IsPasswordComplete()) GameOver(true);
        }
        // Basılan tuşu pasif yap (bir daha basılmasın)
        DisableKeyButton(letter);
    }

    // Tüm harfler bilindi mi kontrol ediyorum
    bool IsPasswordComplete() => currentPassword.All(c => guessedLetters.Contains(c));

    // Ekrana "F A _ İ _" gibi güncel durumu yazdırıyorum
    void UpdateDisplay()
    {
        string display = "";
        foreach (char c in currentPassword)
            display += guessedLetters.Contains(c) ? $"<color=green>{c}</color> " : "_ ";

        passwordDisplayText.text = display;
        teamCountText.text = $"🕵️ Ekip: {teamMembersLeft}/{maxTeamSize}";
    }

    // Seçilen harfin butonunu bulup tıklanmaz hale getiriyorum. DisableKeyButton üzerinden çağrılır
    void DisableKeyButton(char letter)
    {
        var btn = keyboardButtons.FirstOrDefault(b => b.GetComponentInChildren<Text>().text == letter.ToString());
        if (btn != null) btn.interactable = false;
    }

    // Yeni oyun için tüm klavye tuşlarını tekrar aktif ediyorum/resetliyorum
    void ResetKeyboard()
    {
        foreach (Button btn in keyboardButtons) btn.interactable = true;
    }

    // ========================================================================
    //              --- GÖRSEL VE DOSYA İŞLEMLERİ ---
    // ========================================================================

    // Dedektif resimlerini can sayıma göre gösterip gizliyorum
    void ResetTeamVisuals()
    {
        for (int i = 0; i < teamMemberImages.Length; i++)
        {
            bool shouldShow = (i < maxTeamSize);
            teamMemberImages[i].gameObject.SetActive(shouldShow);
            teamMemberImages[i].color = Color.white;
        }
    }

    // Can kaybedince bir dedektifi sahneden siliyorum (Fade out efektiyle)
    void RemoveTeamMember()
    {
        int memberIndex = maxTeamSize - teamMembersLeft - 1;
        if (memberIndex >= 0 && memberIndex < teamMemberImages.Length)
        {
            StartCoroutine(FadeOutImage(teamMemberImages[memberIndex]));
        }
    }

    // Resmin yavaşça kaybolmasını sağlayan Coroutine
    IEnumerator FadeOutImage(Image img)
    {
        for (float t = 0; t < 1; t += Time.deltaTime * 2)
        {
            img.color = new Color(1, 1, 1, 1 - t);
            yield return null;
        }
        img.gameObject.SetActive(false);
    }

    // Kelime listesini döndürür, boşsa varsayılanı verir En başta verdiğimiz default değerler buranın bozulması halinde kullanılıyoer
    string[] GetCurrentWordList()
    {
        if (cityLevels[currentLevelIndex].wordList?.Length > 0)
            return cityLevels[currentLevelIndex].wordList;
        return defaultPasswordList;
    }

    // Resources klasöründeki .txt dosyasından kelimeleri okuyorum
    void LoadWordsFromFile()
    {
        CityLevel level = cityLevels[currentLevelIndex];
        if (!string.IsNullOrEmpty(level.wordFileName))
        {
            TextAsset wordFile = Resources.Load<TextAsset>(level.wordFileName);
            if (wordFile != null)
            {
                // Dosyayı satır satır okuyup, boşlukları silip diziye atıyorum
                level.wordList = wordFile.text.Split('\n')
                    .Select(s => s.Trim().ToUpper())
                    .Where(s => s.Length > 0 && !s.StartsWith("#"))
                    .ToArray();
            }
            else
            {
                Debug.LogWarning($"Dosya bulunamadı: {level.wordFileName}. Varsayılan liste kullanılacak.");
            }
        }
    }
}