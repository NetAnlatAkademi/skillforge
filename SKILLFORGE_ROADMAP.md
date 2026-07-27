# SkillForge — Local CLI Roadmap

> **Amaç:** Agent Skills için yerel çalışan, açık kaynak bir CLI geliştirmek.  
> İlk sürüm; skill oluşturma, doğrulama, inceleme, paketleme ve raporlama yeteneklerine odaklanır.  
> Marketplace, web paneli, ödeme sistemi ve kurumsal registry bu aşamanın kapsamı dışındadır.

---

## 1. Proje Özeti

SkillForge; AI agent skill dosyalarının standartlara uygun, güvenli, test edilebilir ve sürümlenebilir şekilde yönetilmesini sağlayacak bir araç setidir.

İlk ürün:

```text
Local CLI
  ↓
Skill Parser
  ↓
Validator
  ↓
Inspector
  ↓
Packager
  ↓
JSON / SARIF Reports
  ↓
GitHub Action
```

İlk hedef kullanıcılar:

- AI agent skill geliştiren bireysel geliştiriciler
- Yazılım ekipleri
- Platform ve DevOps ekipleri
- AI governance ve güvenlik ekipleri
- Codex, Claude Code, GitHub Copilot ve benzeri agent araçlarını kullanan ekipler

---

## 2. Ürün Tezi

SkillForge bir “skill marketplace” olarak başlamamalıdır.

Ürünün ilk değer önerisi:

> Agent skill dosyalarını oluştur, doğrula, incele, paketle ve CI sürecinde güvenilir şekilde kontrol et.

Uzun vadeli değer önerisi:

> Agent Skills için güvenilir yazılım tedarik zinciri.

---

## 3. İlk Sürümün Kapsamı

### Dahil

- `SKILL.md` dosyasını bulma ve okuma
- YAML frontmatter ayrıştırma
- Skill klasör yapısını inceleme
- Standart alanları doğrulama
- Dosya referanslarını kontrol etme
- Skill kalitesiyle ilgili uyarılar üretme
- Permission ve davranış özeti çıkarma
- JSON raporu oluşturma
- SARIF raporu oluşturma
- Skill paketleme
- Paket hash üretme
- Yerel CLI kurulumu
- GitHub Action entegrasyonuna uygun komut yapısı

### Hariç

- Web paneli
- Public marketplace
- Private registry
- Kullanıcı ve organizasyon yönetimi
- Auth0
- Ödeme sistemi
- Model tabanlı eval çalıştırma
- Docker sandbox
- Uzak paket deposu
- MCP server yönetimi
- Merkezi policy engine
- Telemetri servisi
- Kubernetes
- Mikroservis mimarisi

---

## 4. Teknik Kararlar

### Ana teknoloji

```text
.NET 10
C#
System.CommandLine
YamlDotNet
FluentValidation
xUnit
FluentAssertions
Spectre.Console
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging
```

### Dağıtım biçimi

İlk aşamada:

```bash
dotnet tool install --global SkillForge.Cli
```

Geliştirme sırasında:

```bash
dotnet run --project src/SkillForge.Cli -- validate ./samples/dotnet-api-review
```

### Mimari yaklaşım

- Modüler monolit
- Clean Architecture’ın sadeleştirilmiş hali
- Domain bağımsız CLI katmanı
- Dosya sistemi erişimi abstraction üzerinden
- Tüm doğrulama kuralları bağımsız test edilebilir olmalı
- Komut sınıflarında iş mantığı bulunmamalı
- İlk sürümde database kullanılmamalı

---

## 5. Repository Yapısı

```text
skillforge/
├── README.md
├── LICENSE
├── CHANGELOG.md
├── Directory.Build.props
├── Directory.Packages.props
├── SkillForge.sln
├── docs/
│   ├── architecture.md
│   ├── skillforge-manifest-rfc.md
│   ├── validation-rules.md
│   └── cli-reference.md
├── samples/
│   ├── valid-skill/
│   ├── invalid-frontmatter/
│   ├── broken-references/
│   └── dotnet-api-review/
├── src/
│   ├── SkillForge.Cli/
│   ├── SkillForge.Application/
│   ├── SkillForge.Domain/
│   ├── SkillForge.Infrastructure/
│   └── SkillForge.Reporting/
└── tests/
    ├── SkillForge.Domain.Tests/
    ├── SkillForge.Application.Tests/
    ├── SkillForge.Infrastructure.Tests/
    ├── SkillForge.Reporting.Tests/
    └── SkillForge.Cli.Tests/
```

---

## 6. Proje Katmanları

### SkillForge.Domain

Sorumluluklar:

- Skill modeli
- Validation result modelleri
- Severity değerleri
- Rule result
- Permission modeli
- Package metadata
- Hash bilgileri
- Hata kodları

Örnek modeller:

```csharp
public sealed record SkillDefinition(
    string Name,
    string Description,
    string DirectoryPath,
    string SkillFilePath,
    SkillFrontmatter Frontmatter,
    IReadOnlyList<SkillResource> Resources);

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    int? Line = null,
    string? Suggestion = null);
```

### SkillForge.Application

Sorumluluklar:

- Skill yükleme akışı
- Validation orchestration
- Inspect akışı
- Pack akışı
- Rapor üretme orchestration
- Use case servisleri

Örnek servisler:

```text
ISkillLoader
ISkillValidator
ISkillInspector
ISkillPackager
IReportGenerator
IHashCalculator
```

### SkillForge.Infrastructure

Sorumluluklar:

- Dosya sistemi erişimi
- YAML parsing
- ZIP oluşturma
- SHA-256 hash hesaplama
- Path normalizasyonu
- Glob eşleştirme

### SkillForge.Reporting

Sorumluluklar:

- Console output
- JSON report
- SARIF report
- İnsan tarafından okunabilir özet

### SkillForge.Cli

Sorumluluklar:

- Komut tanımları
- Parametre ve seçenekler
- Exit code
- Dependency Injection bootstrap
- Global exception handling
- Console output yönlendirme

---

## 7. CLI Komutları

İlk sürümde aşağıdaki komutlar geliştirilecektir.

---

### 7.1 `skillforge init`

Yeni bir skill klasörü oluşturur.

```bash
skillforge init my-skill
```

Seçenekler:

```bash
skillforge init my-skill \
  --description "Reviews ASP.NET Core APIs" \
  --author "Çağrı" \
  --license MIT
```

Oluşturulacak yapı:

```text
my-skill/
├── SKILL.md
├── skillforge.yaml
├── references/
├── scripts/
├── assets/
└── evals/
```

Kabul kriterleri:

- Geçerli bir `SKILL.md` oluşturmalı
- Skill adı klasör adıyla uyumlu olmalı
- Var olan klasör üzerine varsayılan olarak yazmamalı
- `--force` seçeneği olmadan overwrite yapmamalı
- Çıktı sonraki `validate` komutundan hatasız geçmeli

---

### 7.2 `skillforge validate`

Skill’in standart ve kalite kurallarına uygunluğunu doğrular.

```bash
skillforge validate ./my-skill
```

Seçenekler:

```bash
skillforge validate ./my-skill --format console
skillforge validate ./my-skill --format json
skillforge validate ./my-skill --format sarif
skillforge validate ./my-skill --output ./artifacts/report.json
skillforge validate ./my-skill --strict
```

Exit code:

```text
0 = Başarılı, hata yok
1 = Validation error
2 = Geçersiz CLI kullanımı
3 = Beklenmeyen uygulama hatası
```

Kabul kriterleri:

- Hataları ve uyarıları ayırmalı
- Her diagnostic benzersiz bir kod taşımalı
- JSON çıktısı makine tarafından işlenebilir olmalı
- `--strict` kullanıldığında warning durumunda exit code 1 dönmeli
- Hatalı YAML uygulamayı çökertmemeli
- Eksik `SKILL.md` anlaşılır hata vermeli

---

### 7.3 `skillforge inspect`

Skill hakkında özet bilgi üretir.

```bash
skillforge inspect ./my-skill
```

Örnek çıktı:

```text
Skill: dotnet-api-review
Description: Reviews ASP.NET Core APIs for quality and security

Files:
  SKILL.md
  references/api-versioning.md
  scripts/analyze.ps1

Detected capabilities:
  Filesystem Read
  Shell Execution

External URLs:
  https://learn.microsoft.com/

Risk indicators:
  1 warning
  0 errors
```

Seçenekler:

```bash
skillforge inspect ./my-skill --format json
skillforge inspect ./my-skill --show-files
skillforge inspect ./my-skill --show-links
skillforge inspect ./my-skill --show-permissions
```

---

### 7.4 `skillforge pack`

Skill klasörünü dağıtılabilir paket haline getirir.

```bash
skillforge pack ./my-skill
```

Oluşturulacak çıktı:

```text
artifacts/
├── my-skill.1.0.0.skill.zip
├── my-skill.1.0.0.skill.zip.sha256
└── my-skill.1.0.0.manifest.json
```

Paketleme öncesi:

- Validation çalışmalı
- Error varsa paketleme durmalı
- `--skip-validation` sadece açıkça verilirse kullanılmalı
- Dosyalar deterministik sırada paketlenmeli
- Paket hash’i SHA-256 olmalı

Seçenekler:

```bash
skillforge pack ./my-skill --output ./dist
skillforge pack ./my-skill --version 1.0.0
skillforge pack ./my-skill --skip-validation
```

---

## 8. `SKILL.md` Beklentileri

Örnek:

```markdown
---
name: dotnet-api-review
description: Reviews ASP.NET Core APIs for architecture, security, performance and backward compatibility.
license: MIT
compatibility:
  - codex
  - claude-code
metadata:
  author: skillforge
  version: 1.0.0
allowed-tools:
  - filesystem.read
---

# .NET API Review

Use this skill when reviewing ASP.NET Core APIs.

## Goals

- Detect backward compatibility risks.
- Review authentication and authorization.
- Identify performance bottlenecks.
- Suggest actionable improvements.

## Workflow

1. Inspect API endpoints.
2. Review contracts.
3. Review authentication.
4. Review persistence access.
5. Produce findings ordered by severity.
```

---

## 9. `skillforge.yaml` Taslağı

```yaml
schemaVersion: 1

package:
  version: 0.1.0
  publisher: local

compatibility:
  agents:
    - codex
    - claude-code
    - github-copilot

permissions:
  filesystem:
    read: []
    write: []
  shell:
    allowed: []
  network:
    allowed: false
  secrets: []

validation:
  strict: false

packageOptions:
  include:
    - "SKILL.md"
    - "references/**"
    - "scripts/**"
    - "assets/**"
    - "evals/**"
  exclude:
    - ".git/**"
    - "bin/**"
    - "obj/**"
    - ".DS_Store"
```

İlk sürümde bu dosya zorunlu olmamalı.

Yoksa:

- Varsayılan davranış kullanılmalı
- Validation warning üretmemeli
- `pack` komutu güvenli varsayılan include/exclude kuralları kullanmalı

---

## 10. Validation Kuralları

Her kural sabit bir diagnostic code taşımalıdır.

### Zorunlu kurallar

| Kod | Seviye | Kural |
|---|---|---|
| SF0001 | Error | `SKILL.md` bulunamadı |
| SF0002 | Error | YAML frontmatter bulunamadı |
| SF0003 | Error | YAML frontmatter parse edilemedi |
| SF0004 | Error | `name` alanı eksik |
| SF0005 | Error | `description` alanı eksik |
| SF0006 | Error | Skill adı geçersiz |
| SF0007 | Error | Referans verilen dosya bulunamadı |
| SF0008 | Error | Path skill klasörü dışına çıkıyor |
| SF0009 | Error | Aynı metadata alanı birden fazla tanımlanmış |
| SF0010 | Error | Paket sürümü geçersiz |

### Kalite kuralları

| Kod | Seviye | Kural |
|---|---|---|
| SF1001 | Warning | Description çok kısa |
| SF1002 | Warning | Description aktivasyon bağlamı belirtmiyor |
| SF1003 | Warning | `SKILL.md` 500 satırdan uzun |
| SF1004 | Warning | Kullanılmayan dosya var |
| SF1005 | Warning | Harici URL bulunuyor |
| SF1006 | Warning | Script dosyası bulunuyor ancak permission tanımlı değil |
| SF1007 | Warning | Shell komutu geniş yetki istiyor |
| SF1008 | Warning | Paket bağımlılıkları sabitlenmemiş |
| SF1009 | Warning | Lisans tanımlanmamış |
| SF1010 | Warning | Agent compatibility bilgisi tanımlanmamış |

### Bilgi kuralları

| Kod | Seviye | Kural |
|---|---|---|
| SF2001 | Info | Skill içerisinde script bulunuyor |
| SF2002 | Info | Skill içerisinde harici URL bulunuyor |
| SF2003 | Info | Skill içerisinde binary dosya bulunuyor |
| SF2004 | Info | Skill içerisinde eval klasörü bulunuyor |

---

## 11. İlk Güvenlik Kontrolleri

İlk sürüm tam güvenlik scanner’ı değildir.

Ancak aşağıdaki sinyaller tespit edilmelidir:

### Shell kalıpları

```text
curl ... | bash
wget ... | sh
rm -rf
Invoke-Expression
powershell -EncodedCommand
chmod 777
sudo
docker run --privileged
```

### Dosya sistemi riskleri

```text
../
~
/etc/
C:\Windows\
.env
.ssh
credentials
secrets
```

### Ağ sinyalleri

- `http://`
- `https://`
- Bilinmeyen domain
- Dosya indirme
- Webhook çağrısı
- IP adresine doğrudan bağlantı

### Secret sinyalleri

```text
API_KEY
TOKEN
PASSWORD
SECRET
PRIVATE_KEY
CONNECTION_STRING
```

Bu kontroller yalnızca diagnostic üretmelidir.

Otomatik olarak “güvenli” veya “zararlı” kararı verilmemelidir.

---

## 12. JSON Rapor Şeması

```json
{
  "schemaVersion": "1.0",
  "tool": {
    "name": "SkillForge",
    "version": "0.1.0"
  },
  "skill": {
    "name": "dotnet-api-review",
    "path": "./skills/dotnet-api-review",
    "version": "1.0.0"
  },
  "summary": {
    "errors": 0,
    "warnings": 2,
    "info": 1,
    "valid": true
  },
  "diagnostics": [
    {
      "code": "SF1009",
      "severity": "warning",
      "message": "License is not defined.",
      "filePath": "SKILL.md",
      "line": 1,
      "suggestion": "Add a license field to the frontmatter."
    }
  ]
}
```

---

## 13. SARIF Desteği

Amaç:

- GitHub Code Scanning entegrasyonu
- Pull Request üzerinde annotation gösterimi
- CI süreçlerinde standart raporlama

İlk sürüm:

- SARIF 2.1.0
- Diagnostic code → ruleId
- Severity mapping
- File ve line mapping
- Rule açıklaması
- Suggestion bilgisi

---

## 14. Konsol Çıktısı

Konsol çıktısı:

- Renkli olmalı
- CI ortamında renk kapatılabilmeli
- Hatalar en üstte gösterilmeli
- Sonunda özet bulunmalı
- `--quiet` ve `--verbose` seçenekleri desteklenmeli

Örnek:

```text
SkillForge Validate

Skill: dotnet-api-review
Path:  ./skills/dotnet-api-review

✓ SKILL.md found
✓ Frontmatter parsed
✓ Required fields valid
⚠ SF1009 License is not defined
⚠ SF1003 SKILL.md has 642 lines

Result: VALID WITH WARNINGS
Errors: 0  Warnings: 2  Info: 0
```

---

## 15. Test Stratejisi

### Unit test

- YAML parser
- Name validation
- Path traversal kontrolü
- Diagnostic üretimi
- Severity mapping
- Hash üretimi
- Version parsing
- Package include/exclude

### Integration test

- Gerçek klasörden skill yükleme
- Geçerli skill doğrulama
- Hatalı skill doğrulama
- JSON raporu
- SARIF raporu
- ZIP paketleme
- CLI exit code

### Snapshot test

Aşağıdaki çıktılar snapshot ile test edilebilir:

- JSON report
- SARIF report
- Console report
- Manifest JSON

### Minimum hedef

```text
Domain coverage: %90+
Application coverage: %80+
Infrastructure coverage: %70+
CLI smoke tests: zorunlu
```

Coverage tek başına başarı kriteri değildir.

Kritik kuralların tamamı test edilmelidir.

---

## 16. Kodlama Standartları

### Genel

- Nullable reference types açık
- Warnings as errors açık
- `var` yalnızca tip açıkça anlaşılıyorsa
- Public API’lerde XML documentation
- CancellationToken desteklenmeli
- Async metot adları `Async` ile bitmeli
- File I/O async kullanılmalı
- Domain katmanında framework bağımlılığı olmamalı
- Static helper kullanımını sınırlı tut
- Exception’ları akış kontrolü için kullanma
- Magic string yerine sabit veya value object kullan

### Sonuç modeli

Beklenen validation hatalarında exception yerine result modeli kullanılmalıdır.

```csharp
public sealed record OperationResult<T>(
    bool IsSuccess,
    T? Value,
    IReadOnlyList<Diagnostic> Diagnostics);
```

### Zaman ve tarih

- UTC kullanılmalı
- Paket manifestinde ISO 8601
- Testlerde doğrudan `DateTime.UtcNow` kullanılmamalı
- `TimeProvider` kullanılmalı

### Dosya sistemi

- Test edilebilir abstraction
- Path normalizasyonu zorunlu
- Symlink ve path traversal dikkate alınmalı
- Skill kök klasörü dışına erişim engellenmeli

---

## 17. Branch ve Commit Stratejisi

Branch:

```text
main
feature/init-command
feature/validate-command
feature/json-report
feature/sarif-report
feature/pack-command
```

Commit örnekleri:

```text
feat(cli): add init command
feat(validation): validate skill frontmatter
feat(reporting): add SARIF output
fix(packaging): prevent path traversal
test(validation): cover missing description rule
docs(cli): document validate command
```

---

## 18. Definition of Done

Bir görev tamamlanmış sayılabilmesi için:

- Kod derleniyor
- Unit testleri geçiyor
- İlgili integration testleri geçiyor
- Yeni public API dokümante edildi
- Diagnostic code eklendiyse dokümana işlendi
- CLI yardım metni güncellendi
- Hata mesajı kullanıcıya anlamlı
- Linux ve Windows path davranışı dikkate alındı
- Yeni bağımlılık gerekçelendirilmiş
- Secret veya kişisel bilgi repository’ye eklenmemiş

---

## 19. Fazlara Göre Yol Haritası

---

### Faz 0 — Bootstrap

Amaç: Repository ve temel mühendislik altyapısını oluşturmak.

Görevler:

- [ ] Git repository oluştur
- [ ] `.gitignore` ekle
- [ ] `LICENSE` ekle
- [ ] `README.md` oluştur
- [ ] Solution ve projeleri oluştur
- [ ] `Directory.Build.props` ekle
- [ ] Central Package Management ekle
- [ ] Nullable ve warnings-as-errors aç
- [ ] xUnit test projelerini oluştur
- [ ] CI build workflow ekle
- [ ] Code formatting ayarlarını ekle
- [ ] İlk architecture dokümanını oluştur

Başarı kriteri:

```bash
dotnet restore
dotnet build
dotnet test
```

komutlarının temiz şekilde çalışması.

---

### Faz 1 — Skill Loader

Amaç: Skill klasörünü güvenilir şekilde okuyabilmek.

Görevler:

- [ ] Skill root tespiti
- [ ] `SKILL.md` bulma
- [ ] Frontmatter ayırma
- [ ] YAML parse
- [ ] Markdown body okuma
- [ ] Resource dosyalarını listeleme
- [ ] Path normalizasyonu
- [ ] Symlink davranışı
- [ ] Loader diagnostics
- [ ] Unit ve integration testleri

Başarı kriteri:

- Geçerli skill modeli oluşturuluyor
- Hatalı YAML uygulamayı çökertmiyor
- Skill klasörü dışına çıkış engelleniyor

---

### Faz 2 — Validation Engine

Amaç: Kuralları bağımsız ve genişletilebilir şekilde çalıştırmak.

Önerilen interface:

```csharp
public interface ISkillValidationRule
{
    string Code { get; }

    ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken);
}
```

Görevler:

- [ ] Rule discovery
- [ ] Rule execution pipeline
- [ ] Required field kuralları
- [ ] Name format kuralları
- [ ] Description kuralları
- [ ] File reference kuralları
- [ ] Path traversal kuralları
- [ ] Length kuralları
- [ ] License ve compatibility uyarıları
- [ ] Summary hesaplama
- [ ] Strict mode

Başarı kriteri:

- Her rule bağımsız test edilebilir
- Bir rule hata verdiğinde diğerleri çalışmaya devam eder
- Diagnostic sırası deterministik olur

---

### Faz 3 — CLI Foundation

Amaç: Kullanılabilir komut satırı deneyimi oluşturmak.

Görevler:

- [ ] Root command
- [ ] Global options
- [ ] DI bootstrap
- [ ] Logging
- [ ] Exception handler
- [ ] Exit code mapping
- [ ] Console renderer
- [ ] `--verbose`
- [ ] `--quiet`
- [ ] `--no-color`
- [ ] Help metinleri
- [ ] CLI smoke tests

Başarı kriteri:

```bash
skillforge --help
skillforge --version
skillforge validate --help
```

çalışır.

---

### Faz 4 — `init`

Görevler:

- [ ] Template modeli
- [ ] Klasör oluşturma
- [ ] Frontmatter üretme
- [ ] `skillforge.yaml` üretme
- [ ] `--force`
- [ ] Geçersiz isim kontrolü
- [ ] Existing directory kontrolü
- [ ] Init sonrası otomatik validation

Başarı kriteri:

```bash
skillforge init sample-skill
skillforge validate sample-skill
```

hatasız çalışır.

---

### Faz 5 — `validate`

Görevler:

- [ ] Console output
- [ ] JSON output
- [ ] Output file
- [ ] Strict mode
- [ ] Exit codes
- [ ] Summary
- [ ] Diagnostic ordering
- [ ] CI friendly output

Başarı kriteri:

- Geçerli sample exit code 0
- Hatalı sample exit code 1
- Strict warning exit code 1
- JSON schema stabil

---

### Faz 6 — `inspect`

Görevler:

- [ ] File inventory
- [ ] External URL extraction
- [ ] Script detection
- [ ] Permission inference
- [ ] Risk indicator summary
- [ ] JSON output
- [ ] Human-readable output

Başarı kriteri:

- Skill’in davranış yüzeyi tek komutla görülebilir
- Çıktı güvenlik garantisi verdiğini iddia etmez

---

### Faz 7 — `pack`

Görevler:

- [ ] Version resolution
- [ ] Include/exclude
- [ ] Deterministic ZIP
- [ ] SHA-256
- [ ] Manifest
- [ ] Validation gate
- [ ] Output directory
- [ ] Existing artifact handling
- [ ] Cross-platform path support

Başarı kriteri:

- Aynı içerik aynı hash’i üretir
- Paket skill root dışından dosya içermez
- Hatalı skill varsayılan olarak paketlenmez

---

### Faz 8 — SARIF ve GitHub Action Hazırlığı

Görevler:

- [ ] SARIF 2.1.0 generator
- [ ] Rule metadata
- [ ] Location mapping
- [ ] GitHub annotation uyumluluğu
- [ ] Örnek workflow
- [ ] CI dokümantasyonu

Örnek workflow:

```yaml
name: SkillForge

on:
  pull_request:
  push:
    branches:
      - main

jobs:
  validate-skills:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - run: dotnet tool install --global SkillForge.Cli

      - run: >
          skillforge validate ./skills
          --format sarif
          --output ./artifacts/skillforge.sarif
```

---

## 20. İlk Milestone

Milestone adı:

```text
v0.1.0 — Local Validator
```

İçerik:

- `init`
- `validate`
- `inspect`
- `pack`
- Console output
- JSON output
- SARIF output
- 20 validation rule
- 4 sample skill
- Global tool packaging
- Linux ve Windows testleri
- CI build

Başarı ölçütü:

> Bir geliştirici, projeyi klonladıktan sonra 10 dakika içinde bir skill oluşturup doğrulayabilmeli.

---

## 21. İkinci Milestone

Milestone adı:

```text
v0.2.0 — Security Signals
```

İçerik:

- Shell pattern detection
- External URL detection
- Secret reference detection
- Permission inference
- Risk summary
- Rule suppression
- Configurable validation
- GitHub Action release

Bu milestone başlamadan önce `v0.1.0` gerçek kullanıcılarla denenmelidir.

---

## 22. Üçüncü Milestone

Milestone adı:

```text
v0.3.0 — Local Evals
```

Planlanan komutlar:

```bash
skillforge eval ./my-skill
skillforge eval ./my-skill --case activation-positive
skillforge eval ./my-skill --format json
```

İlk eval sürümü yalnızca format ve deterministic assertion desteği içerebilir.

Model entegrasyonları daha sonra eklenmelidir.

---

## 23. AI Agent İçin Çalışma Kuralları

Bu repository üzerinde çalışan yapay zekâ aşağıdaki kurallara uymalıdır.

### Genel davranış

1. Büyük değişikliklerden önce mevcut yapıyı incele.
2. Aynı işi yapan yeni abstraction oluşturma.
3. Kapsam dışı özellik ekleme.
4. Web paneli veya registry geliştirmeye başlama.
5. Yeni NuGet paketi eklemeden önce gerekçesini yaz.
6. Her görevde minimum gerekli değişikliği yap.
7. Public API değişikliğinde testleri ve dokümantasyonu güncelle.
8. Hata mesajlarını son kullanıcı perspektifinden yaz.
9. Güvenlik kontrollerinde kesin güvenlik iddiasında bulunma.
10. Cross-platform path davranışını test et.

### Her görev öncesi

AI aşağıdakileri belirtmelidir:

```text
- Değiştirilecek dosyalar
- Uygulanacak yaklaşım
- Olası riskler
- Test planı
```

### Her görev sonrası

AI aşağıdakileri raporlamalıdır:

```text
- Yapılan değişiklikler
- Çalıştırılan testler
- Sonuç
- Kalan riskler
- Sonraki önerilen görev
```

### Yasaklar

- Tüm projeyi tek dosyada toplama
- Validation rule’larını command sınıfına yazma
- Dosya sistemi erişimini doğrudan her yerde kullanma
- Test yazmadan kritik parser geliştirme
- `catch (Exception) { }` kullanma
- Hard-coded işletim sistemi path’i kullanma
- Gereksiz mikroservis oluşturma
- Database ekleme
- Docker zorunluluğu getirme
- Kullanıcının açık talebi olmadan roadmap dışına çıkma

---

## 24. AI İçin İlk Görev

Yerel AI agent’a verilecek ilk komut:

```text
Bu repository için Faz 0 — Bootstrap görevlerini uygula.

Kurallar:
- .NET 10 kullan.
- Solution ve proje yapısını roadmap’e göre oluştur.
- Nullable ve warnings-as-errors açık olsun.
- Central Package Management kullan.
- xUnit ve FluentAssertions ekle.
- README içine build ve test komutlarını ekle.
- Henüz CLI komutu veya business logic geliştirme.
- Değişikliklerden sonra dotnet restore, dotnet build ve dotnet test çalıştır.
- Yaptığın değişiklikleri dosya bazında özetle.
```

---

## 25. Sonraki AI Görevi

Bootstrap tamamlandıktan sonra:

```text
Faz 1 — Skill Loader görevlerini uygula.

Önce domain modellerini ve interface’leri tasarla.
Ardından SKILL.md dosyasını güvenli şekilde okuyacak loader geliştir.
YAML frontmatter ve Markdown body ayrıştırılmalı.
Path traversal ve skill root dışına çıkış engellenmeli.
Geçerli, eksik ve bozuk skill örnekleri için testler yaz.
CLI komutu geliştirme.
```

---

## 26. Ürün Kararları

Karar kayıtları:

### ADR-001

```text
CLI önce geliştirilecek.
Web uygulaması ürün doğrulanmadan başlamayacak.
```

### ADR-002

```text
.NET 10 ve modüler monolit kullanılacak.
```

### ADR-003

```text
SKILL.md standardı değiştirilmeden desteklenecek.
SkillForge’a özel alanlar skillforge.yaml dosyasında tutulacak.
```

### ADR-004

```text
İlk sürümde database kullanılmayacak.
```

### ADR-005

```text
Validation ve inspect işlemleri tamamen yerel çalışacak.
```

### ADR-006

```text
Security sonucu “safe/unsafe” olarak gösterilmeyecek.
Bunun yerine somut diagnostic ve risk sinyalleri sunulacak.
```

---

## 27. Uzun Vadeli Yön

Local CLI doğrulandıktan sonra sıralama:

```text
Local CLI
  ↓
GitHub Action
  ↓
Security Scanner
  ↓
Local Eval Runner
  ↓
Public Scan Report
  ↓
Private Registry
  ↓
Organization Policies
  ↓
Enterprise Governance
  ↓
Verified Catalog
```

Her faz, gerçek kullanıcı ihtiyacı doğrulanmadan başlatılmamalıdır.

---

## 28. Başarı Tanımı

İlk sürüm başarılı sayılırsa:

- 10 farklı repository’de kullanılmıştır.
- En az 20 gerçek skill doğrulanmıştır.
- Kullanıcılar en az bir gerçek validation hatası yakalamıştır.
- CLI kurulum ve ilk kullanım süresi 10 dakikadan kısadır.
- En az 3 kullanıcı ikinci kez kullanmıştır.
- En az bir ekip GitHub Action entegrasyonu talep etmiştir.

---

## 29. Başlangıç Komutları

```bash
mkdir skillforge
cd skillforge

dotnet new sln -n SkillForge

dotnet new console -n SkillForge.Cli -o src/SkillForge.Cli
dotnet new classlib -n SkillForge.Domain -o src/SkillForge.Domain
dotnet new classlib -n SkillForge.Application -o src/SkillForge.Application
dotnet new classlib -n SkillForge.Infrastructure -o src/SkillForge.Infrastructure
dotnet new classlib -n SkillForge.Reporting -o src/SkillForge.Reporting

dotnet new xunit -n SkillForge.Domain.Tests -o tests/SkillForge.Domain.Tests
dotnet new xunit -n SkillForge.Application.Tests -o tests/SkillForge.Application.Tests
dotnet new xunit -n SkillForge.Infrastructure.Tests -o tests/SkillForge.Infrastructure.Tests
dotnet new xunit -n SkillForge.Reporting.Tests -o tests/SkillForge.Reporting.Tests
dotnet new xunit -n SkillForge.Cli.Tests -o tests/SkillForge.Cli.Tests
```

Bu komutlar referans amaçlıdır.

Yerel AI agent, repository durumunu incelemeden körlemesine çalıştırmamalıdır.

---

## 30. Ekosistem Girdileri ve Revize Öncelik (2026-07-27)

Kaynak: `agent-skills-mcp-ekosistem-ozeti.txt` (harici bir sohbet özeti).

> **Doğrulama notu.** Bu bölümdeki dış dünya iddiaları — MCP 2026-07-28 sürümünün RC'sinin kilitlenmesi,
> SkillSec-Eval çalışması, 31.132 skill üzerinde %26,1 güvenlik problemi / %5,2 kötü niyet göstergesi,
> script içeren skill'lerin 2,12 kat riskli olması, sağlayıcıların ürün duyuruları — **ikinci eldendir ve
> bu repoda doğrulanmamıştır.** Yön tayini için kullanılabilirler; bir yatırımı, bir pazarlama iddiasını
> veya bir kural şiddetini gerekçelendirmek için kullanılacaklarsa önce birincil kaynaklarına bakılmalıdır.
> Oranları belgeye taşımak onları doğrulamak değildir.

### 30.1 Ürün konumu

Ekosistem, skill'i kurulabilir bir dağıtım birimine dönüştürüyor: sağlayıcılar (Codex, Claude Code, Cursor,
Copilot) kurulumu, keşfi ve içe aktarmayı kendileri yapıyor. Dolayısıyla **"bir başka kurulum yapan CLI"
olmak zayıf bir konum.** Güçlü konum:

> Sağlayıcılar skill'i kurar. SkillForge kurulmadan önce doğrular, davranış yüzeyini gösterir, değişimini
> raporlar ve uyumluluğunu test eder.

Bu, §2'deki ürün teziyle çelişmiyor; onu daraltıp keskinleştiriyor. Ürünün adı artık şu üçlü:
**Agent Skill Security, Compatibility ve CI.** Public katalog hâlâ ilk hedef değil.

### 30.2 `skillforge diff` — mevcut roadmap'te olmayan, en yüksek değerli komut

```bash
skillforge diff origin/main...HEAD
skillforge diff ./before ./after
```

Amaç, "dosya değişti mi" değil **"davranış yüzeyi değişti mi"**:

```text
Skill behavior changed: dotnet-api-review
  Permissions:      + filesystem.read
  External domains: + api.example.com
  Scripts:          + scripts/analyze.ps1
  Activation scope: broadened
  Evals:            3/4 passed
```

Neden yüksek değerli: bir skill'in izin yüzeyinin sessizce genişlemesi, bir PR'da gözden kaçan ve
gözden kaçtığında en pahalı olan şeydir. GitHub, agent skill'lerini PR sürecine soktuğu için bu bilgi tam
olarak PR yorumunda durması gereken bilgi.

Mimari kanca: `diff` iki `SkillDefinition` ile çalışır ve `inspect`'in zaten ürettiği yüzeyi (dosyalar,
URL'ler, script'ler, bildirilen araçlar, açıklama/aktivasyon metni) karşılaştırır. Yeni bir okuma katmanı
gerekmez; gereken, iki sürümü yükleyebilmek (git revizyonundan veya iki dizinden) ve yüzey farkını
modellemektir.

### 30.3 Güvenlik: tek katman değil, yaşam döngüsü

Risk yalnızca "script çalıştırma" anında değil; şu aşamaların hepsinde doğuyor: repository admission,
semantic retrieval, planner selection, execution, skill evolution. Bu yüzden scanner regex tabanlı shell
taramasından fazlası olmalı. Risk modeli yedi katman:

1. Package provenance
2. Activation manipulation
3. Instruction security
4. Permission surface
5. Executable content
6. External communication
7. Version-to-version behavior change

### 30.4 Yeni diagnostic bantları

| Bant | Kapsam |
|---|---|
| `SF3xxx` | Activation ve retrieval riskleri |
| `SF4xxx` | Instruction injection riskleri |
| `SF5xxx` | Supply-chain ve provenance riskleri |
| `SF6xxx` | Sürüm ve evrim (davranış değişimi) riskleri |

**Kayda geçen karar (bu bir duruş değişikliğidir).** v0.1.0 boyunca kod kümesi bilinçli olarak 24'te
sabit tutuldu; okunamayan bir `SKILL.md` için 25. bir kod uydurmak yerine SF0001'in anlamı genişletildi
(`docs/validation-rules.md`). Bu bantlar o kısıtı **açıkça** kaldırıyor: kod kümesi kapalı değil,
*yayınlanmış kodların anlamı ve şiddeti* sabittir. Yeni kod eklemek serbest; var olanı yeniden
anlamlandırmak değil.

### 30.5 Sandbox yeterli sınır değil

İleride eklenecek sandbox scanner şu varsayımla tasarlanmamalıdır: *"container içinde çalıştıysa
güvenlidir."* Coding agent'ların, repo içindeki kötü amaçlı README/bağımlılık/yapılandırma içerikleri
üzerinden host tarafındaki IDE, Git ve extension bileşenlerini etkileyip sandbox dışına çıkabildiği
bildirildi. Bu yüzden çalıştırma sırasında ayrıca izlenmesi gerekenler:

- Çalışma öncesi/sonrası repository diff
- Git config değişiklikleri
- IDE / agent config değişiklikleri
- Hook oluşturma
- Symlink oluşturma
- Workspace dışına yazma
- Sonraki çalıştırmayı etkileyen kalıcı dosyalar

### 30.6 MCP: adapter'lar, çekirdeğe gömme yok

MCP'nin 2026-07-28 sürümü oturum/initialize el sıkışmasını kaldırıp stateless HTTP'ye geçiyor, `Mcp-Method`
ve `Mcp-Name` başlıklarıyla gateway desteği ekliyor, Tasks extension ve MCP Apps getiriyor, OAuth/OIDC'yi
sıkılaştırıyor, Roots / Sampling / Logging'i deprecation'a alıyor, tool şemalarında JSON Schema 2020-12
istiyor.

Protokol bu hızda değişiyorsa **CLI çekirdeği bir protokol sürümüne bağlanmamalıdır.** Yapı:

```text
Skill Analyzer  (çekirdek, protokolden bağımsız)
  ├── MCP 2025-11-25 Adapter
  └── MCP 2026-07-28 Adapter
```

`inspect`'in ileride MCP için raporlaması gerekenler: kullanılan protokol sürümü, deprecated capability
kullanımı, stateful transport bağımlılığı, authorization yöntemi, tool schema uyumluluğu.

### 30.7 `skillforge migrate inspect`

Sağlayıcılar arası taşınabilirlik gerçek bir kullanıcı ihtiyacına dönüştüğü için (Codex CLI'ın Cursor ve
Claude Code'dan ayar, MCP sunucusu, plugin, komut ve proje hafızası içe aktarabilmesi), Cursor / Claude Code
/ Codex / Copilot yapılandırmalarını okuyup şunları raporlayan bir komut:

- Skill envanteri
- MCP envanteri
- Çakışan talimatlar
- Kayıp bağımlılıklar
- Sağlayıcı uyumsuzlukları

### 30.8 Revize milestone sırası

| Sürüm | İçerik |
|---|---|
| **v0.1** | `init`, `validate`, `inspect`, `pack`, SARIF — **tamamlandı** |
| **v0.2** | `skillforge diff`, activation-risk kuralları, permission inference, harici URL / script analizi, GitHub Action, PR annotation'ları |
| **v0.3** | Local evals, pozitif/negatif aktivasyon testleri, sağlayıcı uyumluluğu, Codex / Claude / Copilot adapter'ları |
| **v0.4** | Migration envanteri, MCP protokol incelemesi, MCP 2025 ve 2026 adapter'ları, deprecated capability tespiti |

§21'deki v0.2.0 tanımı bu tabloyla değiştirilmiştir: `diff` ve activation-risk kuralları eklendi, sıralama
"security signals"tan "security + CI" ekseninine kaydı. §27'deki uzun vadeli sıra da buna göre okunmalıdır:
GitHub Action ve Security Scanner, Private Registry'den önce gelir.

### 30.9 Bu girdinin değiştirmediği şeyler

- **ADR-006 aynen geçerli.** Yedi katmanlı risk modeli, "safe/unsafe" kararı vermek anlamına gelmez.
  Katmanlar daha fazla ve daha iyi *sinyal* demektir; hüküm hâlâ okuyucunun.
- **ADR-001 aynen geçerli.** Bu girdi web panelini değil, CLI + Action eksenini güçlendiriyor.
- **Ölçülmüş gerçeklik hâlâ üstün.** 229 gerçek skill üzerinde SF1009/SF1010'un neredeyse her skill'de
  ateşlendiği ve SF0008'in kardeş skill referanslarını hata sayması ölçülmüş bulgulardır
  (`docs/validation-rules.md`). Yeni kural bantları eklenirken aynı hata tekrarlanmamalı: **bir kuralı
  yayınlamadan önce gerçek skill'ler üzerinde ateşlenme oranı ölçülmelidir.** Neredeyse her girdide
  ateşlenen bir kural sinyal değil gürültüdür.

---

## 31. Son Not

Bu projenin ilk hedefi büyük bir platform kurmak değildir.

İlk hedef:

> Agent skill geliştiren bir yazılımcının, hatalı veya riskli bir skill’i birkaç saniye içinde fark etmesini sağlayan güvenilir bir CLI üretmek.

Ürün büyüdükçe platform özellikleri eklenecektir.

İlk aşamada kalite, sadelik ve geliştirici deneyimi; özellik sayısından daha önemlidir.
