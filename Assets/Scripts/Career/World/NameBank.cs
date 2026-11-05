using System;
using System.Collections.Generic;

/// Where a name comes from, for a squad's player names (NameBank.RegionOf maps Nations codes).
public enum eNameRegion
{
    SouthAsian, AngloAntipodean, Caribbean, SouthernAfrican, AfghanPakistani, SriLankan, Bangladeshi,
    DutchScotsIrish, NorthAmerican,
}

/// <summary>
/// The names of the career's world: players and sides, all fictional (store release: no real
/// teams, leagues, players or brands). A player's name is a common first name and a common
/// surname of his region, so it reads right on a scorecard, and the pair is never one of the
/// famous cricketers in Famous (a club side with a "Virat Kohli" in it would read as the real man).
/// Sides: village clubs at Club, invented regions at State, invented cities with original
/// nicknames at Pro League - checked against the real franchise and county names in RealTeams and
/// RealNicknames - and the nations themselves at International. Everything is plain ASCII, inside
/// the Latin-1 the TMP default font draws. Names and lists live in NameLists.
/// </summary>
public static class NameBank
{
    public struct TeamName
    {
        public string name, shortName;
        public TeamName(string name, string shortName) { this.name = name; this.shortName = shortName; }
    }

    public static eNameRegion RegionOf(string nation) => nation switch
    {
        "IND" or "NEP" or "UAE" => eNameRegion.SouthAsian,
        "AUS" or "ENG" or "NZ" => eNameRegion.AngloAntipodean,
        "WI" => eNameRegion.Caribbean,
        "RSA" or "ZIM" => eNameRegion.SouthernAfrican,
        "AFG" or "PAK" => eNameRegion.AfghanPakistani,
        "SL" => eNameRegion.SriLankan,
        "BAN" => eNameRegion.Bangladeshi,
        "NED" or "SCO" or "IRE" => eNameRegion.DutchScotsIrish,
        "USA" or "CAN" => eNameRegion.NorthAmerican,
        _ => eNameRegion.SouthAsian,
    };

    /// A player's name from `nation`'s region: never a famous cricketer's, and never one in `used`
    /// (pass the names already in the league, or null). Adds the name to `used`.
    public static string Player(string nation, Random rng, HashSet<string> used = null)
    {
        int r = (int)RegionOf(nation);
        string[] first = NameLists.First[r], last = NameLists.Last[r];
        string name = "";
        for (int tries = 0; tries < 200; tries++)
        {
            name = first[rng.Next(first.Length)] + " " + last[rng.Next(last.Length)];
            if (IsFamous(name)) continue;
            if (used != null && used.Contains(name)) continue;
            break;
        }
        // 200 misses only if the region's names are nearly all used: a numbered name is still unique.
        if (IsFamous(name) || (used != null && used.Contains(name))) name = Unique(name, used);
        used?.Add(name);
        return name;
    }

    private static string Unique(string name, HashSet<string> used)
    {
        for (int i = 2; ; i++)
        {
            string n = name + " " + Roman(i);
            if (!IsFamous(n) && (used == null || !used.Contains(n))) return n;
        }
    }

    private static string Roman(int i) => i switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => i.ToString() };

    /// The name as a scorecard shows it: everything after the first name ("de Silva", "O'Neill").
    public static string Surname(string full)
    {
        if (string.IsNullOrEmpty(full)) return "";
        string t = full.Trim();
        int sp = t.IndexOf(' ');
        return sp > 0 && sp < t.Length - 1 ? t.Substring(sp + 1) : t;
    }

    /// One of the famous cricketers no generated player may share a name with (case-insensitive).
    public static bool IsFamous(string full)
    {
        string n = (full ?? "").Trim();
        foreach (string f in NameLists.Famous)
            if (string.Equals(f, n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// The pool of side names for a domestic rung (Club, State, Pro League). International sides
    /// are the nations (NationTeam).
    public static TeamName[] Teams(eRung rung) => rung switch
    {
        eRung.Club => NameLists.Clubs,
        eRung.State => NameLists.States,
        _ => NameLists.Franchises,
    };

    /// A nation's side: its name and a three-letter scoreboard code ("NZL", not "NZ").
    public static TeamName NationTeam(string code)
    {
        string c = Nations.All[Nations.IndexOf(code)].code;
        string shortName = c switch { "NZ" => "NZL", "SL" => "SRI", "WI" => "WIN", _ => c };
        return new TeamName(Nations.Name(c), shortName);
    }

    /// A real side's full name or a nickname a real franchise or county side uses is in `name`
    /// (case-insensitive, whole words for nicknames). Every generated side name must be false.
    public static bool ImitatesReal(string name)
    {
        string n = " " + (name ?? "").ToLowerInvariant() + " ";
        foreach (string r in NameLists.RealTeams)
            if (n.Contains(r.ToLowerInvariant())) return true;
        foreach (string r in NameLists.RealNicknames)
            if (n.Contains(" " + r.ToLowerInvariant() + " ")) return true;
        foreach (string r in NameLists.RealCities)
            if (n.Contains(" " + r.ToLowerInvariant() + " ")) return true;
        return false;
    }

    /// Every character is in Latin-1 (the TMP default font's range).
    public static bool IsLatin1(string s)
    {
        foreach (char c in s ?? "")
            if (c > 255) return false;
        return true;
    }
}

/// <summary>
/// NameBank's lists: first names and surnames by region (indexed by eNameRegion), the famous
/// cricketers never to generate, the side-name pools, and the real side names, nicknames and
/// franchise cities a generated side must never imitate (NameBank.ImitatesReal). First names and
/// surnames are common ones, chosen away from the famous pairs; the Famous list catches the
/// pairs that are still reachable and the iconic names besides (LeagueTests checks both).
/// </summary>
public static class NameLists
{
    public static readonly string[][] First =
    {
        // South Asian (India, Nepal, UAE)
        new[] { "Arjun", "Rohan", "Karan", "Vikram", "Aditya", "Siddharth", "Nikhil", "Amit", "Sanjay", "Vivek",
                "Pranav", "Harsh", "Kunal", "Manish", "Tarun", "Varun", "Anil", "Gaurav", "Akash", "Naveen",
                "Pradeep", "Rajesh", "Suresh", "Dev", "Kabir", "Neeraj", "Sameer", "Abhay", "Yash", "Ritesh",
                "Omkar", "Sunil", "Bikash", "Sagar", "Prakash" },
        // Anglo / Antipodean (England, Australia, New Zealand)
        new[] { "James", "Tom", "Ben", "Sam", "Harry", "Oliver", "Jack", "Will", "Matt", "Luke",
                "Ryan", "Josh", "Liam", "Callum", "Dan", "Nick", "Adam", "George", "Charlie", "Ollie",
                "Jake", "Ethan", "Max", "Hamish", "Angus", "Toby", "Lachlan", "Hugh", "Archie", "Rob",
                "Alex", "Ed", "Joe", "Finn", "Riley" },
        // Caribbean
        new[] { "Shane", "Devon", "Andre", "Marlon", "Akeem", "Tevin", "Romario", "Jomel", "Kevon", "Ricardo",
                "Dwight", "Delroy", "Winston", "Leroy", "Clyde", "Kirk", "Desmond", "Garfield", "Keon", "Jahmar",
                "Kemol", "Nyeem", "Jerome", "Damion", "Rashawn", "Trevon", "Odane", "Nkosi" },
        // Southern African (South Africa, Zimbabwe)
        new[] { "Pieter", "Johan", "Ruan", "Werner", "Hendrik", "Francois", "Wian", "Thabo", "Sipho", "Lwazi",
                "Tshepo", "Bongani", "Themba", "Lungelo", "Mandla", "Jaco", "Stephan", "Christo", "Farai", "Kudzai",
                "Tafadzwa", "Takudzwa", "Tawanda", "Simba", "Nyasha", "Kgosi", "Sizwe", "Lesego", "Neo", "Kabelo" },
        // Afghan / Pakistani
        new[] { "Ahmed", "Bilal", "Faisal", "Hamza", "Usman", "Zain", "Saad", "Ali", "Tariq", "Waleed",
                "Adnan", "Irfan", "Zubair", "Fahad", "Ahmad", "Habib", "Farid", "Wahid", "Javid", "Zahir",
                "Shafiq", "Khalil", "Nadir", "Omar", "Asim", "Kashif", "Naveed", "Sami", "Nasir", "Daud" },
        // Sri Lankan
        new[] { "Kasun", "Sahan", "Tharindu", "Ruwan", "Asanka", "Chathura", "Dilan", "Nimesh", "Sandun", "Pasindu",
                "Kavindu", "Malith", "Sachith", "Janith", "Hasitha", "Ravindu", "Supun", "Dulaj", "Isuru", "Chamath",
                "Lakshan", "Nadun", "Pramod", "Ashen", "Sanjaya", "Gayan", "Thilina", "Mahesh", "Dimuthu" },
        // Bangladeshi
        new[] { "Rafiq", "Arif", "Sajid", "Rakib", "Tanvir", "Faisal", "Shahriar", "Mahfuz", "Jahid", "Habib",
                "Anwar", "Rezaul", "Tareq", "Shohel", "Mamun", "Rasel", "Jubair", "Nayeem", "Sohag", "Ripon",
                "Fahim", "Imtiaz", "Jamil", "Masud", "Rony" },
        // Dutch / Scots / Irish
        new[] { "Daan", "Ruud", "Joost", "Sander", "Jelle", "Thijs", "Wouter", "Bram", "Niels", "Stijn",
                "Lars", "Koen", "Ewan", "Ross", "Alasdair", "Iain", "Gregor", "Murray", "Euan", "Ciaran",
                "Conor", "Declan", "Padraig", "Cormac", "Darragh", "Ronan", "Fionn", "Seamus", "Aidan", "Tadhg",
                "Rory", "Finlay" },
        // North American (USA, Canada)
        new[] { "Tyler", "Brandon", "Kevin", "Jason", "Eric", "Derek", "Marcus", "Nikhil", "Ravi", "Dilpreet",
                "Harjot", "Jaskaran", "Gurpreet", "Jordan", "Austin", "Cody", "Ethan", "Logan", "Nathan", "Shayan",
                "Aarav", "Mason", "Hunter", "Tanner", "Kieran", "Devin" },
    };

    public static readonly string[][] Last =
    {
        new[] { "Sharma", "Patel", "Iyer", "Nair", "Reddy", "Rao", "Desai", "Joshi", "Kulkarni", "Menon",
                "Pillai", "Bhatt", "Chauhan", "Verma", "Gupta", "Mehta", "Kapoor", "Malhotra", "Saxena", "Thakur",
                "Pandey", "Mishra", "Shetty", "Naidu", "Bose", "Ghosh", "Thapa", "Gurung", "Shrestha", "Karki",
                "Adhikari", "Tamang" },
        new[] { "Harris", "Walker", "Wright", "Thompson", "Edwards", "Hall", "Turner", "Hill", "Cooper", "Ward",
                "King", "Baker", "Parker", "Bennett", "Price", "Cole", "Fletcher", "Hart", "Lane", "Webb",
                "Barnes", "Holt", "Sutton", "Doyle", "Fisher", "Gibson", "Foster", "Lowe", "Mason", "Watts",
                "Nash", "Dixon", "Sharp", "Ashby", "Bramley", "Penrose", "Whitlock", "Ingham", "Rowe", "Tilley",
                "Garside", "Hollis", "Birch", "Pickering", "Ellery", "Mercer" },
        new[] { "Baptiste", "Charles", "Joseph", "Thomas", "Phillip", "Gordon", "Daniel", "Alleyne", "Bascombe", "Gittens",
                "Inniss", "Lashley", "Mottley", "Prescod", "Springer", "Toppin", "Greaves", "Boyce", "Hinds", "Belle",
                "Cumberbatch", "Rollins", "Callender", "Babb", "Forde", "Skeete", "Harewood", "Lovell", "Pilgrim", "Francis",
                "Moseley" },
        new[] { "du Toit", "Venter", "Steenkamp", "Kruger", "Joubert", "Visser", "Fourie", "Swanepoel", "Ndlovu", "Dlamini",
                "Mokoena", "Khumalo", "Nkosi", "Mahlangu", "Moyo", "Ncube", "Sibanda", "Dube", "Mutasa", "Marufu",
                "Nyathi", "Gumbo", "Chivhanga", "van Wyk", "Pienaar", "Cloete", "Erasmus", "Badenhorst", "Mabaso", "Zondo",
                "Molefe", "Radebe" },
        new[] { "Qureshi", "Siddiqui", "Raza", "Iqbal", "Chaudhry", "Mirza", "Sheikh", "Javed", "Aslam", "Haider",
                "Abbasi", "Rana", "Bajwa", "Cheema", "Hashmi", "Safi", "Mangal", "Momand", "Ahmadzai", "Popal",
                "Sultani", "Wardak", "Ghani", "Hotak", "Noori", "Barakzai", "Durrani", "Kakar" },
        new[] { "Perera", "Fernando", "de Silva", "Silva", "Jayasinghe", "Wickramasinghe", "Gunawardena", "Rathnayake",
                "Bandara", "Dissanayake", "Senanayake", "Ranasinghe", "Liyanage", "Wijesinghe", "Samarasinghe",
                "Abeysekera", "Gamage", "Peiris", "Kodikara", "Weerasinghe", "Jayawickrama", "Amarasinghe",
                "Hettiarachchi", "Kumarasinghe", "Munasinghe", "Nanayakkara", "Pathirage", "Wanigasekara", "Ekanayake" },
        new[] { "Hossain", "Islam", "Rahman", "Ahmed", "Haque", "Hasan", "Uddin", "Alam", "Chowdhury", "Sarkar",
                "Mia", "Karim", "Kabir", "Sultan", "Talukder", "Bhuiyan", "Mollah", "Siddique", "Khandaker", "Sikder",
                "Majumder", "Biswas", "Mahmud", "Akand" },
        new[] { "de Vries", "van Dijk", "Bakker", "Jansen", "Meijer", "de Boer", "de Groot", "Bos", "Vos", "Hendriks",
                "Dekker", "van Leeuwen", "Brouwer", "Kok", "MacLeod", "Campbell", "Stewart", "Paterson", "Reid", "Henderson",
                "MacDonald", "Robertson", "Kerr", "Munro", "Crawford", "Buchanan", "Drummond", "Galbraith", "Murphy", "Kelly",
                "Byrne", "Walsh", "Doyle", "Gallagher", "Brennan", "Quinn", "Kavanagh", "Nolan", "Moran", "Dunne",
                "Flanagan", "Hennessy", "Lynch" },
        new[] { "Patel", "Singh", "Gill", "Sandhu", "Dhillon", "Grewal", "Johnson", "Anderson", "Brooks", "Carter",
                "Reyes", "Campbell", "Bailey", "Hayes", "Reid", "Parker", "Bishop", "Coleman", "Warren", "Dixon",
                "Holt", "Sharma", "Brar", "Bajwa", "Sidhu", "Rodriguez", "Lopez", "Nguyen" },
    };

    /// Famous cricketers' full names: never generated. The iconic names of every nation, and the
    /// real players whose names the lists above can still put together.
    public static readonly string[] Famous =
    {
        "Virat Kohli", "Sachin Tendulkar", "MS Dhoni", "Rohit Sharma", "Rahul Dravid", "Sourav Ganguly",
        "Anil Kumble", "Kapil Dev", "Sunil Gavaskar", "Jasprit Bumrah", "Ravindra Jadeja", "Hardik Pandya",
        "Yuvraj Singh", "Shubman Gill", "Rishabh Pant", "Axar Patel", "Parthiv Patel", "Ishant Sharma",
        "Amit Mishra", "Manish Pandey", "Sunil Joshi", "Yash Thakur", "Karan Sharma", "Mohit Sharma",
        "Joe Root", "Ben Stokes", "James Anderson", "Stuart Broad", "Jos Buttler", "Alastair Cook",
        "Kevin Pietersen", "Andrew Flintoff", "Ian Botham", "Luke Wright", "James Foster", "Angus Fraser",
        "Steve Smith", "David Warner", "Pat Cummins", "Mitchell Starc", "Glenn Maxwell", "Ricky Ponting",
        "Shane Warne", "Adam Gilchrist", "Glenn McGrath", "Ryan Harris", "Callum Ferguson", "Tom Cooper",
        "Kane Williamson", "Ross Taylor", "Brendon McCullum", "Trent Boult", "Tim Southee", "Richard Hadlee",
        "Hamish Bennett", "Chris Gayle", "Brian Lara", "Viv Richards", "Garfield Sobers", "Kieron Pollard",
        "Andre Russell", "Sunil Narine", "Jason Holder", "Devon Thomas", "Keon Joseph", "Devon Smith",
        "AB de Villiers", "Jacques Kallis", "Dale Steyn", "Hashim Amla", "Faf du Plessis", "Francois du Plessis",
        "Kagiso Rabada", "Graeme Smith", "Quinton de Kock", "Wiaan Mulder", "Pieter Malan", "Johan Botha",
        "Andy Flower", "Heath Streak", "Sikandar Raza", "Babar Azam", "Wasim Akram", "Waqar Younis",
        "Imran Khan", "Shahid Afridi", "Shaheen Afridi", "Mohammad Rizwan", "Shoaib Akhtar", "Faisal Iqbal",
        "Sami Aslam", "Ali Raza", "Ahmed Raza", "Ali Khan", "Rashid Khan", "Mohammad Nabi", "Zahir Khan",
        "Usman Ghani", "Hamza Hotak", "Kumar Sangakkara", "Mahela Jayawardene", "Muttiah Muralitharan",
        "Sanath Jayasuriya", "Lasith Malinga", "Aravinda de Silva", "Ashen Bandara", "Janith Liyanage",
        "Shakib Al Hasan", "Tamim Iqbal", "Mushfiqur Rahim", "Tanvir Islam", "Nayeem Hasan", "Nayeem Islam",
        "Jubair Hossain", "Rony Talukder", "Rony Talukdar", "Kevin O'Brien", "Paul Stirling", "Calum MacLeod",
        "Kyle Coetzer", "Ryan ten Doeschate", "Jaskaran Singh", "Dilpreet Bajwa", "Gurpreet Singh", "Monank Patel",
    };

    public static readonly NameBank.TeamName[] Clubs =
    {
        new NameBank.TeamName("Harbour CC", "HBR"), new NameBank.TeamName("Old Mill CC", "OMC"),
        new NameBank.TeamName("Riverside CC", "RVS"), new NameBank.TeamName("Kingsbridge CC", "KBR"),
        new NameBank.TeamName("Ashford Vale CC", "ASV"), new NameBank.TeamName("Millbrook CC", "MBK"),
        new NameBank.TeamName("Westfield CC", "WFD"), new NameBank.TeamName("Elm Park CC", "ELM"),
        new NameBank.TeamName("Oakleigh CC", "OAK"), new NameBank.TeamName("Stonebridge CC", "STB"),
        new NameBank.TeamName("Larkhill CC", "LRK"), new NameBank.TeamName("Fernhill CC", "FHL"),
        new NameBank.TeamName("Hollowmere CC", "HLM"), new NameBank.TeamName("Brackenridge CC", "BRK"),
        new NameBank.TeamName("Marston Green CC", "MSG"), new NameBank.TeamName("Cedar Point CC", "CDP"),
        new NameBank.TeamName("Willow Lane CC", "WLL"), new NameBank.TeamName("Copper Beech CC", "CPB"),
    };

    public static readonly NameBank.TeamName[] States =
    {
        new NameBank.TeamName("Northmoor Herons", "NMH"), new NameBank.TeamName("Eastvale Badgers", "EVB"),
        new NameBank.TeamName("Southreach Owls", "SRO"), new NameBank.TeamName("Western Plains Bison", "WPB"),
        new NameBank.TeamName("Lowland Otters", "LWO"), new NameBank.TeamName("Upper Valley Kestrels", "UVK"),
        new NameBank.TeamName("Midland Cranes", "MDC"), new NameBank.TeamName("Coastal Curlews", "CCU"),
        new NameBank.TeamName("Hill Country Merlins", "HCM"), new NameBank.TeamName("Lakeside Ibis", "LKI"),
        new NameBank.TeamName("Red Plateau Swifts", "RPS"), new NameBank.TeamName("Greenfields Plovers", "GFP"),
    };

    public static readonly NameBank.TeamName[] Franchises =
    {
        new NameBank.TeamName("Port Calder Lamplighters", "PCL"), new NameBank.TeamName("Aldermoor Stormcrows", "ASC"),
        new NameBank.TeamName("Kestrel Bay Tidecallers", "KBT"), new NameBank.TeamName("Ironvale Nightjars", "IVN"),
        new NameBank.TeamName("Brightwater Sandpipers", "BWS"), new NameBank.TeamName("Marrowgate Red Kites", "MRK"),
        new NameBank.TeamName("Corranhill Embers", "CHE"), new NameBank.TeamName("Westmarch Wayfarers", "WMW"),
        new NameBank.TeamName("Tamberlyn Monsoons", "TAM"), new NameBank.TeamName("Easthaven Harriers", "EHV"),
        new NameBank.TeamName("Duncairn Ironbarks", "DIB"), new NameBank.TeamName("Saltmere Gannets", "SMG"),
    };

    /// Real franchise, county and state T20 sides (IPL, BBL, PSL, CPL, SA20, The Hundred, MLC,
    /// ILT20, LPL, BPL, county and state competitions): no generated side name contains one.
    public static readonly string[] RealTeams =
    {
        "Mumbai Indians", "Chennai Super Kings", "Royal Challengers", "Kolkata Knight Riders", "Delhi Capitals",
        "Delhi Daredevils", "Punjab Kings", "Kings XI Punjab", "Rajasthan Royals", "Sunrisers Hyderabad",
        "Deccan Chargers", "Gujarat Titans", "Gujarat Lions", "Lucknow Super Giants", "Rising Pune Supergiant",
        "Pune Warriors", "Kochi Tuskers", "Adelaide Strikers", "Brisbane Heat", "Hobart Hurricanes",
        "Melbourne Renegades", "Melbourne Stars", "Perth Scorchers", "Sydney Sixers", "Sydney Thunder",
        "Karachi Kings", "Lahore Qalandars", "Islamabad United", "Peshawar Zalmi", "Quetta Gladiators",
        "Multan Sultans", "Trinbago Knight Riders", "Barbados Royals", "Barbados Tridents", "Guyana Amazon Warriors",
        "Jamaica Tallawahs", "St Kitts and Nevis Patriots", "Saint Lucia Kings", "Saint Lucia Zouks", "Antigua Falcons",
        "MI Cape Town", "Paarl Royals", "Pretoria Capitals", "Durban's Super Giants", "Joburg Super Kings",
        "Sunrisers Eastern Cape", "Birmingham Phoenix", "London Spirit", "Manchester Originals", "Northern Superchargers",
        "Oval Invincibles", "Southern Brave", "Trent Rockets", "Welsh Fire", "Los Angeles Knight Riders",
        "MI New York", "San Francisco Unicorns", "Seattle Orcas", "Texas Super Kings", "Washington Freedom",
        "Abu Dhabi Knight Riders", "Desert Vipers", "Dubai Capitals", "Gulf Giants", "MI Emirates", "Sharjah Warriors",
        "Colombo Strikers", "Galle Titans", "Jaffna Kings", "Kandy Falcons", "Dambulla Aura", "Dhaka Dynamites",
        "Comilla Victorians", "Rangpur Riders", "Sylhet Strikers", "Khulna Tigers", "Fortune Barishal",
        "Lancashire Lightning", "Yorkshire Vikings", "Notts Outlaws", "Birmingham Bears", "Essex Eagles",
        "Kent Spitfires", "Sussex Sharks", "Hampshire Hawks", "Leicestershire Foxes", "Northamptonshire Steelbacks",
        "Worcestershire Rapids", "Derbyshire Falcons", "Auckland Aces", "Northern Knights", "Northern Brave",
        "Central Stags", "Wellington Firebirds", "Canterbury Kings", "Otago Volts", "Queensland Bulls",
        "New South Wales Blues", "Victorian Bushrangers", "Tasmanian Tigers", "Western Warriors", "Redbacks",
        "Cape Cobras", "Hollywoodbets Dolphins", "Scottish Saltires",
    };

    /// Nicknames real franchise and county sides use: no generated side carries one as a word.
    public static readonly string[] RealNicknames =
    {
        "Indians", "Super Kings", "Kings", "Royals", "Knight Riders", "Capitals", "Daredevils", "Sunrisers",
        "Chargers", "Titans", "Lions", "Super Giants", "Supergiant", "Warriors", "Tuskers", "Strikers", "Heat",
        "Hurricanes", "Renegades", "Stars", "Scorchers", "Sixers", "Thunder", "Qalandars", "Zalmi", "Gladiators",
        "Sultans", "United", "Tridents", "Tallawahs", "Patriots", "Zouks", "Falcons", "Phoenix", "Spirit",
        "Originals", "Superchargers", "Invincibles", "Brave", "Rockets", "Fire", "Unicorns", "Orcas", "Freedom",
        "Vipers", "Giants", "Dynamites", "Victorians", "Riders", "Tigers", "Lightning", "Vikings", "Outlaws",
        "Bears", "Eagles", "Spitfires", "Sharks", "Hawks", "Foxes", "Steelbacks", "Rapids", "Aces", "Knights",
        "Stags", "Firebirds", "Volts", "Bulls", "Blues", "Bushrangers", "Redbacks", "Cobras", "Dolphins", "Jets",
        "Saltires", "Challengers", "Stallions",
    };

    /// Cities, counties and states real franchise and domestic sides are named for.
    public static readonly string[] RealCities =
    {
        "Mumbai", "Chennai", "Bengaluru", "Bangalore", "Kolkata", "Delhi", "Hyderabad", "Rajasthan", "Punjab",
        "Gujarat", "Lucknow", "Pune", "Kochi", "Deccan", "Sydney", "Melbourne", "Adelaide", "Brisbane", "Perth",
        "Hobart", "Karachi", "Lahore", "Islamabad", "Peshawar", "Quetta", "Multan", "Trinbago", "Barbados",
        "Guyana", "Jamaica", "Antigua", "Durban", "Paarl", "Pretoria", "Joburg", "Johannesburg", "Cape Town",
        "Birmingham", "London", "Manchester", "Southampton", "Nottingham", "Cardiff", "Oval", "Seattle", "Texas",
        "Washington", "San Francisco", "Los Angeles", "New York", "Dubai", "Abu Dhabi", "Sharjah", "Colombo",
        "Kandy", "Galle", "Jaffna", "Dambulla", "Dhaka", "Chattogram", "Sylhet", "Khulna", "Rangpur", "Barishal",
        "Comilla", "Auckland", "Wellington", "Canterbury", "Otago", "Lancashire", "Yorkshire", "Surrey", "Kent",
        "Essex", "Sussex", "Hampshire", "Somerset", "Durham", "Middlesex", "Notts", "Warwickshire", "Glamorgan",
        "Queensland", "Victoria", "Tasmania", "New South Wales",
    };
}
