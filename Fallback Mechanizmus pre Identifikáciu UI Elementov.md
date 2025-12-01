# Fallback Mechanizmus pre Identifikáciu UI Elementov

## Prehľad Implementácie

Implementovaný bol 7-úrovňový fallback mechanizmus pre spoľahlivú identifikáciu UI elementov naprieč rôznymi aplikáciami.

## Hierarchia Identifikátorov (od najspoľahlivejšieho)

### 1. AutomationId (95% confidence)
- Najstabilnejší identifikátor
- Jedinečný per aplikácia
- **Použitie:** Ako primárny identifikátor

### 2. Name + ControlType (90% confidence)
- Kombinácia názvu a typu
- Filtrovanie generických názvov
- **Použitie:** Keď AutomationId nie je dostupný

### 3. TreePath (80% confidence)
- **NOVÝ** - Hierarchická cesta v UI strome
- Formát: `"Desktop/Window[0]/Panel[2]/Button[1]"`
- Odolný voči presunom okna
- **Použitie:** Keď sa UI štruktúra nemení

### 4. ClassName + Position (70% confidence)
- Kombinácia triedy a relatívnej pozície
- Filtrovanie generických tried
- **Použitie:** Pre elementy s unikátnymi triedami

### 5. Name Match (65% confidence)
- Presný match názvu elementu
- **Použitie:** Fallback pre jednoznačné názvy

### 6. ControlType + Position (60% confidence)
- Typ elementu + pozícia s 30px toleranciou
- **Použitie:** Pre vizuálne stabilné UI

### 7. Fuzzy Name Match (50% confidence)
- Približná zhoda názvov
- **Použitie:** Posledná možnosť pred súradnicami

## Nové Súbory

### ElementIdentifier.cs
Nová trieda obsahujúca:
- `GenerateTreePath()` - Generuje hierarchickú cestu
- `FindByTreePath()` - Nájde element podľa TreePath
- `GetBestIdentifier()` - Vráti najlepší dostupný identifikátor
- Helper metódy pre detekciu generických ID/názvov

## Upravené Súbory

### Command.cs
- Pridané: `ElementTreePath` property
- Aktualizované: `UpdateFromElementInfo()` - ukladá TreePath
- Vylepšené: `CalculateElementConfidence()` - započítava TreePath (25% váha)

### UIElementDetector.cs
- Aktualizované: `ExtractElementInfo()` - generuje TreePath pri detekcii
- Debug logging pre TreePath

### AdaptiveElementFinder.cs
- Prepísané: `SmartFindElement()` - nový 7-úrovňový fallback
- Pridané: `FindByNameAndType()` - kombinácia Name + ControlType
- Pridané: `IsGenericName()`, `IsGenericClassName()` - filtrovanie
- Vylepšené: Debug výstupy pre každú úroveň

## Príklady Použitia

### Jednoduchý button
```
Level 1: AutomationId="btnSave" ✓ Found (95%)
```

### Element bez AutomationId
```
Level 1: AutomationId="" ✗
Level 2: Name="Save" + Type="Button" ✓ Found (90%)
```

### Dynamické UI
```
Level 1-2: ✗
Level 3: TreePath="Desktop/Window[0]/ToolBar[0]/Button[2]" ✓ Found (80%)
```

### Generický element
```
Level 1-3: ✗
Level 4: ClassName="CustomButton" + Position(250,150) ✓ Found (70%)
```

## Výhody

1. **Spoľahlivosť:** TreePath odolný voči presunom okna
2. **Flexibilita:** 7 úrovní fallback pre rôzne scenáre
3. **Transparentnosť:** Debug logging pre každú úroveň
4. **Kompatibilita:** Zachovaná spätná kompatibilita

## Maintenance

- TreePath sa generuje automaticky pri nahrávaní
- Ukladá sa do .acc súborov
- Používa sa automaticky pri prehrávaní
- Žiadne zmeny v užívateľskom rozhraní

## Testy

**Otestovať:**
1. Nahrať sequence v aplikácii s AutomationId
2. Nahrať sequence v aplikácii bez AutomationId (napr. staršia Win32 app)
3. Presunúť okno a prehrať sequence
4. Zmeniť veľkosť okna a prehrať sequence

## Zhrnutie

**Hierarchia (od najspoľahlivejšieho):**

- AutomationId (95%)
- Name + ControlType (90%)
- TreePath - hierarchická cesta napr. "Window[0]/Panel[2]/Button[1]" (80%)
- ClassName + Position (70%)
- Name Match (65%)
- ControlType + Position (60%)
- Fuzzy Name (50%)
- Súradnice - len ako posledná možnosť

TreePath je odolný voči presunom okna, ale zachytí zmeny v UI štruktúre - ideálny balans.