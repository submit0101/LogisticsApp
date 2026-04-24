using System;
using System.Collections.Generic;
using LogisticsApp.Models;
using LogisticsApp.Services.Interfaces;
using LogisticsApp.ViewModels.Windows;
using LogisticsApp.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Services.Implementations;

public class HelpService : IHelpService
{
    private readonly Dictionary<string, HelpDocument> _helpData = new();
    private readonly IServiceProvider _serviceProvider;

    public HelpService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeHelpData();
    }

    private void InitializeHelpData()
    {
        _helpData["HomeViewModel"] = new HelpDocument
        {
            ModuleTitle = "Главный экран системы",
            IconKind = "Home",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Главный экран предназначен для сводной информации о системе, статусе подключения и версии программного обеспечения." },
                new HelpSection { Title = "Навигация", Content = "Используйте левое боковое меню для переключения между модулями. Доступность модулей зависит от вашей роли и назначенных прав доступа." }
            }
        };

        _helpData["OrdersViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Заказы покупателей",
            IconKind = "CartOutline",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Модуль предназначен для управления заказами покупателей, контроля их статусов и формирования отгрузочных документов." },
                new HelpSection { Title = "Кнопка '+ СОЗДАТЬ ЗАКАЗ'", Content = "Открывает форму нового заказа. Выберите клиента, склад отгрузки и добавьте товары. Если товара не хватает на складе, система предложит создать заявку на пополнение (кнопка 'СДЕЛАТЬ ЗАПРОС НА СКЛАД')." },
                new HelpSection { Title = "Кнопка 'РЕДАКТИРОВАТЬ'", Content = "Позволяет изменить состав и реквизиты заказа. Редактирование заблокировано, если заказ уже привязан к путевому листу." },
                new HelpSection { Title = "Кнопка 'УДАЛИТЬ'", Content = "Перемещает заказ в 'Корзину'. Проведенные заказы удалить нельзя — сначала необходимо отменить проведение." },
                new HelpSection { Title = "Умный поиск и Фильтры", Content = "Позволяют быстро найти заказ по номеру, названию клиента, статусу или дате. Таблица обновляется автоматически при изменении фильтров." },
                new HelpSection { Title = "ОТЧЕТЫ И ПЕЧАТЬ", Content = "Формирует Excel-отчет со списком заказов, отфильтрованных за выбранный период, с группировкой по контрагентам и подсчетом итоговых сумм." },
                new HelpSection { Title = "Статусы заказов", Content = "Новый - создан и готов к логистике. В плане - привязан к черновику путевого листа. В пути - машина выехала. Доставлен - рейс успешно завершен." }
            }
        };

        _helpData["WaybillsViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Путевые листы",
            IconKind = "MapMarkerPath",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Основной модуль диспетчера. Позволяет формировать маршруты, назначать автомобили и водителей, контролировать расход ГСМ и фиксировать ЧП на рейсе." },
                new HelpSection { Title = "Кнопка '+ СОЗДАТЬ ПУТЕВОЙ ЛИСТ'", Content = "Открывает редактор путевого листа. Во вкладке 'Точки маршрута' вы добавляете заказы. Система автоматически считает вес и объем, предупреждая о перегрузе ТС." },
                new HelpSection { Title = "Кнопка 'ОПТИМИЗАЦИЯ' (Внутри карточки)", Content = "Автоматически выстраивает добавленные заказы в оптимальный маршрут с учетом реальной дорожной сети (API OSRM), минимизируя холостой пробег." },
                new HelpSection { Title = "Кнопка 'НАЧАТЬ РЕЙС / ЗАВЕРШИТЬ РЕЙС'", Content = "Переводит П/Л в статус 'В пути'. По завершении рейса происходит фактическое списание товаров со склада и начисление дебиторской задолженности клиентам." },
                new HelpSection { Title = "Кнопка 'КАРТА'", Content = "Открывает отдельное окно с интерактивной картой (GMap.NET), на которой отрисован точный маршрут следования автомобиля." },
                new HelpSection { Title = "ОТЧЕТЫ И ПЕЧАТЬ", Content = "Позволяет распечатать Товарно-транспортную накладную (ТТН) в PDF, Маршрутный лист экспедитора в Excel и сгенерировать этикетки с QR-кодами для груза." },
                new HelpSection { Title = "Вкладка 'ГСМ и Заправки'", Content = "Внутри карточки П/Л можно вносить чеки с АЗС. Система автоматически высчитывает нормативный расход на основе базовой нормы, веса груза и зимних надбавок." }
            }
        };

        _helpData["InventoryViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Склад и Запасы",
            IconKind = "Warehouse",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Управление товарными остатками в разрезе складов (WMS). Модуль отображает как фактическое наличие, так и зарезервированные под заказы объемы." },
                new HelpSection { Title = "Вкладка 'Сводка остатков'", Content = "Показывает агрегированные данные: Доступно (свободно для продажи), В резерве (ожидают отгрузки) и Фактический остаток." },
                new HelpSection { Title = "Вкладка 'Документы движения'", Content = "История всех операций: оприходования, ручные списания и автоматически сгенерированные заявки на пополнение." },
                new HelpSection { Title = "Кнопка '+ СОЗДАТЬ ДОКУМЕНТ ОПРИХОДОВАНИЯ'", Content = "Открывает окно прихода товаров на склад. Поддерживается учет в различных упаковках (система автоматически пересчитает их в базовые единицы)." },
                new HelpSection { Title = "ОТЧЕТЫ И ПЕЧАТЬ", Content = "Позволяет выгрузить Оборотно-сальдовую ведомость (ОСВ) за период, а также Анализ дефицита (сравнение потребности по заказам и наличия на складах)." }
            }
        };

        _helpData["FinanceViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Казначейство и Финансы",
            IconKind = "CashRegister",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Контроль взаиморасчетов с клиентами, регистрация входящих платежей и управление кредиторской/дебиторской задолженностью." },
                new HelpSection { Title = "Вкладка 'Взаиморасчеты'", Content = "Отображает сальдо по каждому клиенту. Система автоматически определяет, должен ли клиент нам (Долг), либо у него есть свободные средства (Аванс)." },
                new HelpSection { Title = "Кнопка '+ ЗАРЕГИСТРИРОВАТЬ ПЛАТЕЖ'", Content = "Создает Приходный кассовый ордер (ПКО). Платеж можно привязать к конкретному заказу или зачислить как общий аванс." },
                new HelpSection { Title = "Автоматический зачет авансов", Content = "При проведении путевого листа (завершении доставки) система проверяет наличие свободных авансов у клиента и автоматически списывает долг за заказ." },
                new HelpSection { Title = "ФИНАНСОВЫЕ ОТЧЕТЫ", Content = "Позволяет сформировать классический Акт сверки взаиморасчетов в Excel по выбранному контрагенту за любой период, а также Реестр поступлений ДДС." }
            }
        };

        _helpData["VehiclesViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Автопарк",
            IconKind = "TruckOutline",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Ведение базы данных транспортных средств компании, учет технических характеристик и нормативов расхода топлива." },
                new HelpSection { Title = "Кнопка '+ ДОБАВИТЬ ТС'", Content = "Открывает карточку автомобиля. Вкладка 'Учет ГСМ' содержит критически важные коэффициенты (надбавка за груз, зима), которые влияют на списание топлива в рейсах." },
                new HelpSection { Title = "История обслуживания", Content = "Внутри карточки ТС можно фиксировать заказ-наряды на ремонт и ТО, указывая пробег, стоимость и механика." },
                new HelpSection { Title = "Срок санобработки", Content = "Система жестко контролирует дату последней санобработки. Если с момента обработки прошло более 30 дней, выпуск автомобиля в рейс может быть заблокирован (в зависимости от настроек)." }
            }
        };

        _helpData["DriversViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Водители",
            IconKind = "CardAccountDetailsOutline",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Кадровый учет водителей и контроль сроков действия их документов (ВУ и Медицинские справки)." },
                new HelpSection { Title = "Индикатор предупреждения (!)", Content = "Красный значок в таблице сигнализирует о том, что у водителя скоро истекает или уже истек срок действия документов. За сколько дней выводить предупреждение — задается в Настройках." },
                new HelpSection { Title = "Кнопка '+ ДОБАВИТЬ ВОДИТЕЛЯ'", Content = "Позволяет завести нового сотрудника, загрузить его фото, указать категории прав и паспортные данные." },
                new HelpSection { Title = "Статусы", Content = "При увольнении сотрудника не рекомендуется удалять его из базы (это приведет к ошибкам в истории рейсов). Переведите его в статус 'Уволен'." }
            }
        };

        _helpData["CustomersViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Контрагенты",
            IconKind = "StorefrontOutline",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Управление базой клиентов (B2B и B2C). Содержит юридические и фактические адреса для точной маршрутизации." },
                new HelpSection { Title = "Автозаполнение по ИНН", Content = "Внутри карточки контрагента достаточно ввести ИНН и нажать 'Заполнить'. Система подключится к API ФНС (DaData) и сама внесет название, КПП, ОГРН, адреса и руководителя." },
                new HelpSection { Title = "Уточнение координат", Content = "Во вкладке 'Логистика (Карта)' в карточке клиента можно дважды кликнуть по карте, чтобы вручную установить точные координаты рампы разгрузки. Это радикально улучшит работу автомаршрутизатора в путевых листах." },
                new HelpSection { Title = "ИМПОРТ ИЗ EXCEL", Content = "Позволяет массово загрузить базу клиентов из внешнего файла. Система автоматически распознает столбцы 'ИНН', 'Наименование' и 'Адрес'." }
            }
        };

        _helpData["NomenclatureViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Номенклатура",
            IconKind = "PackageVariant",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Управление справочником товаров (SKU), ценообразованием и весогабаритными характеристиками (ВГХ) упаковок." },
                new HelpSection { Title = "Иерархия (Дерево групп)", Content = "Слева отображается древовидная структура групп. При клике на папку в таблице справа покажутся только товары из этой ветки." },
                new HelpSection { Title = "Упаковки и ВГХ", Content = "В карточке товара можно задать различные упаковки (Штуки, Коробки, Паллеты) и их весогабаритные данные. При добавлении товара в заказ или на склад система автоматически конвертирует количество через заданные коэффициенты." },
                new HelpSection { Title = "История цен", Content = "Цены задаются на конкретную дату. При оформлении заказа система сама подставит цену, актуальную на дату заказа." },
                new HelpSection { Title = "ЭКСПОРТ В EXCEL", Content = "Формирует красивый прайс-лист для печати или отправки клиентам. Если выделена конкретная папка, система предложит выгрузить только ее." }
            }
        };

        _helpData["ReportsViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Аналитика и Отчеты",
            IconKind = "ChartBar",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Аналитический центр. Содержит сводные дашборды и табличные отчеты для оценки KPI автопарка и логистики." },
                new HelpSection { Title = "Аналитика Тонно-километры", Content = "Рассчитывает коммерческую эффективность каждого рейса: вес груза умножается на пробег с грузом (от точки выезда до последней точки разгрузки)." },
                new HelpSection { Title = "Аналитика Показания одометров", Content = "Сводит данные о пробеге (Начало/Конец периода) для списания ГСМ в бухгалтерии." },
                new HelpSection { Title = "Графики", Content = "Вкладка 'Графики и Аналитика' визуализирует сформированную выборку данных (ТОП-10 машин по пробегу, структура расхода и т.д.)." }
            }
        };

        _helpData["SettingsViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Настройки системы",
            IconKind = "Cog",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение модуля", Content = "Глобальные параметры приложения, влияющие на бизнес-логику и интерфейс." },
                new HelpSection { Title = "Политики и Безопасность", Content = "Позволяет менять допустимый процент перегруза автомобиля (для защиты от штрафов), срок тайм-аута неактивности и строгость проверки санобработки." },
                new HelpSection { Title = "API и Интеграции", Content = "Здесь задается токен DaData.ru для работы автозаполнения ИНН и геокодинга. Без этого ключа умная маршрутизация работать не будет." },
                new HelpSection { Title = "База данных", Content = "Инструмент для создания бэкапов (.bak) и настройки подключения к SQL Server." }
            }
        };

        _helpData["UsersViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Управление Пользователями",
            IconKind = "AccountGroup",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Модуль администратора для создания учетных записей сотрудников и назначения им ролей." },
                new HelpSection { Title = "Безопасность", Content = "Пароли хранятся в базе данных в виде зашифрованных хэшей (SHA-256 с солью). Удалить самого себя или единственного администратора система не позволит." }
            }
        };

        _helpData["RolesViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Управление Ролями (RBAC)",
            IconKind = "Security",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Модуль Role-Based Access Control. Позволяет тонко настраивать доступы к различным модулям, кнопкам и функциям выгрузки." },
                new HelpSection { Title = "Системные роли", Content = "Роль 'Администратор' защищена от редактирования. Она имеет безусловный доступ ко всей системе на уровне ядра." }
            }
        };

        _helpData["ArchiveViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Корзина и Архив",
            IconKind = "DeleteRestore",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Когда пользователи удаляют объекты в интерфейсе, они попадают сюда (Soft Delete). Из корзины объекты можно восстановить или удалить физически." },
                new HelpSection { Title = "Автоочистка", Content = "Система автоматически удаляет старые записи из корзины по истечении срока, заданного в Настройках (ArchiveRetentionDays)." }
            }
        };

        _helpData["AuditViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Журнал Аудита БД",
            IconKind = "History",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Теневое логирование. Система автоматически фиксирует каждое добавление, изменение или удаление записей в БД с указанием автора и времени." }
            }
        };

        _helpData["LogViewerViewModel"] = new HelpDocument
        {
            ModuleTitle = "Справка: Системный журнал (Logs)",
            IconKind = "Console",
            Sections = new List<HelpSection>
            {
                new HelpSection { Title = "Назначение", Content = "Анализ текстовых файлов логов Serilog. Предназначен для разработчиков и IT-отдела для расследования критических ошибок и сбоев интеграций." }
            }
        };
    }

    public void ShowHelpForModule(string moduleName)
    {
        if (_helpData.TryGetValue(moduleName, out var document))
        {
            var vm = _serviceProvider.GetRequiredService<HelpViewModel>();
            vm.Initialize(document);

            var window = _serviceProvider.GetRequiredService<HelpWindow>();
            window.DataContext = vm;
            window.ShowDialog();
        }
    }
}