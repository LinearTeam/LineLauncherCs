using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class I18nTools
    {
        /*
         * 已废弃
         *从 0 开始，依次往下
            简体中文(中国)        zh_CN
            繁体中文(台湾地区)     zh_TW
            繁体中文(香港)        zh_HK
            英语(香港)            en_HK
            英语(美国)            en_US
            英语(英国)            en_GB
            英语(全球)    en_WW
            英语(加拿大)    en_CA
            英语(澳大利亚)    en_AU
            英语(爱尔兰)    en_IE
            英语(芬兰)    en_FI
            芬兰语(芬兰)    fi_FI
            英语(丹麦)    en_DK
            丹麦语(丹麦)    da_DK
            英语(以色列)    en_IL
            希伯来语(以色列)    he_IL
            英语(南非)    en_ZA
            英语(印度)    en_IN
            英语(挪威)    en_NO
            英语(新加坡)    en_SG
            英语(新西兰)    en_NZ
            英语(印度尼西亚)    en_ID
            英语(菲律宾)    en_PH
            英语(泰国)    en_TH
            英语(马来西亚)    en_MY
            英语(阿拉伯)    en_XA
            韩文(韩国)    ko_KR
            日语(日本)    ja_JP
            荷兰语(荷兰)    nl_NL
            荷兰语(比利时)    nl_BE
            葡萄牙语(葡萄牙)    pt_PT
            葡萄牙语(巴西)    pt_BR
            法语(法国)    fr_FR
            法语(卢森堡)    fr_LU
            法语(瑞士)    fr_CH
            法语(比利时)    fr_BE
            法语(加拿大)    fr_CA
            西班牙语(拉丁美洲)    es_LA
            西班牙语(西班牙)    es_ES
            西班牙语(阿根廷)    es_AR
            西班牙语(美国)    es_US
            西班牙语(墨西哥)    es_MX
            西班牙语(哥伦比亚)    es_CO
            西班牙语(波多黎各)    es_PR
            德语(德国)    de_DE
            德语(奥地利)    de_AT
            德语(瑞士)    de_CH
            俄语(俄罗斯)    ru_RU
            意大利语(意大利)    it_IT
            希腊语(希腊)    el_GR
            挪威语(挪威)    no_NO
            匈牙利语(匈牙利)    hu_HU
            土耳其语(土耳其)    tr_TR
            捷克语(捷克共和国)    cs_CZ
            斯洛文尼亚语    sl_SL
            波兰语(波兰)    pl_PL
            瑞典语(瑞典)    sv_SE
            西班牙语 (智利)    es_CL
         */
           
        private static int s_lang = 0;
        private static string s_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/i18n/";
        private static LineFileParser s_lineFileParser = new LineFileParser();

        private static readonly List<string> _locales = new List<string>
        {
            "zh_CN", "zh_TW", "zh_HK", "en_HK", "en_US", "en_GB", "en_WW", "en_CA", "en_AU", "en_IE", "en_FI", "fi_FI",
            "en_DK", "da_DK", "en_IL", "he_IL", "en_ZA", "en_IN", "en_NO", "en_SG", "en_NZ", "en_ID", "en_PH", "en_TH",
            "en_MY", "en_XA", "ko_KR", "ja_JP", "nl_NL", "nl_BE", "pt_PT", "pt_BR", "fr_FR", "fr_LU", "fr_CH", "fr_BE",
            "fr_CA", "es_LA", "es_ES", "es_AR", "es_US", "es_MX", "es_CO", "es_PR", "de_DE", "de_AT", "de_CH", "ru_RU",
            "it_IT", "el_GR", "no_NO", "hu_HU", "tr_TR", "cs_CZ", "sl_SL", "pl_PL", "sv_SE", "es_CL", "Language_Han" //汉化就应该有汉文
        };
        public string GetString(string key)
        {
            return s_lineFileParser.Read($"{s_path + GetLangName()}.line",key,"content");
        }

        public string GetString(string key, int lang)
        {
            return s_lineFileParser.Read($"{s_path + GetLangName(lang)}.line", key, "content");
        }
        public string GetLangName() {
            return _locales[s_lang];
        }
        public string GetLangName(int lang)
        {
            return _locales[lang];
        }
        public void SetLang(int lang)
        {
            I18nTools.s_lang = lang;
        }
        public void SetString(string key, string value)
        {
            s_lineFileParser.Write($"{s_path + GetLangName()}.line", key,value, "content");
        }

        public void SetString(string key, string value, int lang)
        {
            s_lineFileParser.Write($"./lmc/resources/i18n/{GetLangName(lang)}.line", key, value, "content");
        }
    }
}
