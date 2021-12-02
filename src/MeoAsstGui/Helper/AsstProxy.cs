using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using StyletIoC;

namespace MeoAsstGui
{
    public class AsstProxy
    {
        private delegate void CallbackDelegate(int msg, IntPtr json_buffer, IntPtr custom_arg);

        private delegate void ProcCallbckMsg(AsstMsg msg, JObject detail);

        [DllImport("MeoAssistance.dll")] private static extern IntPtr AsstCreate();

        [DllImport("MeoAssistance.dll")] private static extern IntPtr AsstCreateEx(CallbackDelegate callback, IntPtr custom_arg);

        [DllImport("MeoAssistance.dll")] private static extern void AsstDestory(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstCatchDefault(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendFight(IntPtr ptr, int max_medicine, int max_stone, int max_times);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendAward(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendVisit(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendMall(IntPtr ptr, bool with_shopping);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendInfrast(IntPtr ptr, int work_mode, string[] order, int order_len, int uses_of_drones, double dorm_threshold);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstAppendRecruit(IntPtr ptr, int max_times, int[] required_level, int required_len, int[] confirm_level, int confirm_len, bool need_refresh);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstStartRecruitCalc(IntPtr ptr, int[] required_level, int required_len, bool set_time);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstStart(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstStop(IntPtr ptr);

        [DllImport("MeoAssistance.dll")] private static extern bool AsstSetPenguinId(IntPtr p_asst, string id);

        //[DllImport("MeoAssistance.dll")] private static extern bool AsstSetParam(IntPtr p_asst, string type, string param, string value);

        private CallbackDelegate _callback;

        public AsstProxy(IContainer container, IWindowManager windowManager)
        {
            _container = container;
            _windowManager = windowManager;
            _callback = CallbackFunction;
        }

        public void Init()
        {
            _ptr = AsstCreateEx(_callback, IntPtr.Zero);
            if (_ptr == IntPtr.Zero)
            {
                _windowManager.ShowMessageBox("程序初始化错误！请检查是否是因为使用了中文路径", "错误");
                Environment.Exit(0);
            }
        }

        private void CallbackFunction(int msg, IntPtr json_buffer, IntPtr custom_arg)
        {
            string json_str = Marshal.PtrToStringAnsi(json_buffer);
            //Console.WriteLine(json_str);
            JObject json = (JObject)JsonConvert.DeserializeObject(json_str);
            ProcCallbckMsg dlg = new ProcCallbckMsg(proc_msg);
            Execute.OnUIThread(() =>
            {
                dlg((AsstMsg)msg, json);
            });
        }

        private IWindowManager _windowManager;
        private IContainer _container;
        private IntPtr _ptr;

        private void proc_msg(AsstMsg msg, JObject detail)
        {
            var tvm = _container.Get<TaskQueueViewModel>();
            switch (msg)
            {
                case AsstMsg.TaskStart:
                    break;

                case AsstMsg.TaskChainStart:
                    {
                        string taskChain = detail["task_chain"].ToString();
                        tvm.AddLog("开始任务：" + taskChain);
                    }
                    break;

                case AsstMsg.TaskCompleted:
                    {
                        string taskName = detail["name"].ToString();
                        if (taskName == "StartButton2")
                        {
                            tvm.AddLog("已开始行动 " + (int)detail["exec_times"] + " 次");
                        }
                        else if (taskName == "MedicineConfirm")
                        {
                            tvm.AddLog("已吃药 " + (int)detail["exec_times"] + " 个");
                        }
                        else if (taskName == "StoneConfirm")
                        {
                            tvm.AddLog("已碎石 " + (int)detail["exec_times"] + " 颗");
                        }
                    }
                    break;

                case AsstMsg.TaskChainCompleted:
                    {
                        string taskChain = detail["task_chain"].ToString();
                        tvm.AddLog("完成任务：" + taskChain);
                    }
                    break;

                case AsstMsg.AllTasksCompleted:
                    {
                        tvm.AddLog("任务已全部完成");
                        tvm.Idle = true;
                        tvm.CheckAndShutdown();
                    }
                    break;

                case AsstMsg.StageDrops:
                    {
                        string cur_drops = "";
                        JArray drops = (JArray)detail["drops"];
                        foreach (var item in drops)
                        {
                            string itemName = item["itemName"].ToString();
                            int count = (int)item["quantity"];
                            cur_drops += $"{itemName} : {count}\n";
                        }
                        cur_drops = cur_drops.EndsWith("\n") ? cur_drops.TrimEnd('\n') : "无";
                        tvm.AddLog("当次掉落：\n" + cur_drops);

                        string all_drops = "";
                        JArray statistics = (JArray)detail["statistics"];
                        foreach (var item in statistics)
                        {
                            string itemName = item["itemName"].ToString();
                            int count = (int)item["count"];
                            all_drops += $"{itemName} : {count}\n";
                        }
                        all_drops = all_drops.EndsWith("\n") ? all_drops.TrimEnd('\n') : "无";
                        tvm.AddLog("掉落统计：\n" + all_drops);
                    }
                    break;

                case AsstMsg.TextDetected:
                    break;

                case AsstMsg.RecruitTagsDetected:
                case AsstMsg.OcrResultError:
                case AsstMsg.RecruitSpecialTag:
                case AsstMsg.RecruitResult:
                    recruit_proc_msg(msg, detail);
                    break;
                /* Infrast Msg */
                case AsstMsg.InfrastSkillsDetected:
                case AsstMsg.InfrastSkillsResult:
                case AsstMsg.InfrastComb:
                case AsstMsg.EnterFacility:
                case AsstMsg.FacilityInfo:
                    infrast_proc_msg(msg, detail);
                    break;

                case AsstMsg.TaskError:
                    break;

                case AsstMsg.InitFaild:
                    _windowManager.ShowMessageBox("资源文件错误！请尝试重新解压或下载", "错误");
                    Environment.Exit(0);
                    break;
            }
        }

        private void infrast_proc_msg(AsstMsg msg, JObject detail)
        {
            var tvm = _container.Get<TaskQueueViewModel>();
            switch (msg)
            {
                case AsstMsg.InfrastSkillsDetected:
                    break;

                case AsstMsg.InfrastSkillsResult:
                    break;

                case AsstMsg.InfrastComb:
                    break;

                case AsstMsg.EnterFacility:
                    tvm.AddLog("当前设施：" + detail["facility"] + " " + (int)detail["index"]);
                    break;

                case AsstMsg.FacilityInfo:
                    break;

                case AsstMsg.TaskChainCompleted:
                    break;
            }
        }

        private void recruit_proc_msg(AsstMsg msg, JObject detail)
        {
            var rvm = _container.Get<RecruitViewModel>();
            switch (msg)
            {
                case AsstMsg.TextDetected:
                    break;

                case AsstMsg.RecruitTagsDetected:
                    JArray tags = (JArray)detail["tags"];
                    string info_content = "识别结果:    ";
                    foreach (var tag_name in tags)
                    {
                        info_content += tag_name.ToString() + "    ";
                    }
                    rvm.RecruitInfo = info_content;
                    break;

                case AsstMsg.OcrResultError:
                    rvm.RecruitInfo = "识别错误！";
                    break;

                case AsstMsg.RecruitSpecialTag:
                    _windowManager.ShowMessageBox("检测到特殊Tag:" + detail["tag"].ToString(), "提示");
                    break;

                case AsstMsg.RecruitResult:
                    string resultContent = "";
                    JArray result_array = (JArray)detail["result"];
                    int combs_level = 0;
                    foreach (var combs in result_array)
                    {
                        int tag_level = (int)combs["tag_level"];
                        if (tag_level > combs_level)
                        {
                            combs_level = tag_level;
                        }
                        resultContent += tag_level + "星Tags:  ";
                        foreach (var tag in (JArray)combs["tags"])
                        {
                            resultContent += tag.ToString() + "    ";
                        }
                        resultContent += "\n\t";
                        foreach (var oper in (JArray)combs["opers"])
                        {
                            resultContent += oper["level"].ToString() + " - " + oper["name"].ToString() + "    ";
                        }
                        resultContent += "\n\n";
                    }
                    rvm.RecruitResult = resultContent;
                    if (combs_level >= 5)
                    {
                        _windowManager.ShowMessageBox("出 " + combs_level + " 星了哦！", "提示");
                    }
                    break;
            }
        }

        private bool _isCatched = false;

        public bool AsstCatchDefault()
        {
            if (!_isCatched)
            {
                _isCatched = AsstCatchDefault(_ptr);
            }
            return _isCatched;
        }

        public bool AsstAppendFight(int max_medicine, int max_stone, int max_times)
        {
            return AsstAppendFight(_ptr, max_medicine, max_stone, max_times);
        }

        public bool AsstAppendAward()
        {
            return AsstAppendAward(_ptr);
        }

        public bool AsstAppendVisit()
        {
            return AsstAppendVisit(_ptr);
        }

        public bool AsstAppendMall(bool with_shopping)
        {
            return AsstAppendMall(_ptr, with_shopping);
        }

        public bool AsstAppendRecruit(int max_times, int[] required_level, int required_len, int[] confirm_level, int confirm_len, bool need_refresh)
        {
            return AsstAppendRecruit(_ptr, max_times, required_level, required_len, confirm_level, confirm_len, need_refresh);
        }

        public bool AsstAppendInfrast(int work_mode, string[] order, int order_len, int uses_of_drones, double dorm_threshold)
        {
            return AsstAppendInfrast(_ptr, work_mode, order, order_len, uses_of_drones, dorm_threshold);
        }

        public bool AsstStart()
        {
            return AsstStart(_ptr);
        }

        public bool AsstStartRecruitCalc(int[] required_level, int required_len, bool set_time)
        {
            return AsstStartRecruitCalc(_ptr, required_level, required_len, set_time);
        }

        public bool AsstStop()
        {
            return AsstStop(_ptr);
        }

        public void AsstSetPenguinId(string id)
        {
            AsstSetPenguinId(_ptr, id);
        }

        //public void AsstSetParam(string type, string param, string value)
        //{
        //    AsstSetParam(_ptr, type, param, value);
        //}
    }

    public enum AsstMsg
    {
        /* Error Msg */
        PtrIsNull,							// 指针为空
        ImageIsEmpty,						// 图像为空
        WindowMinimized,					// [已弃用] 窗口被最小化了
        InitFaild,							// 初始化失败
        TaskError,							// 任务错误（任务一直出错，retry次数达到上限）
        OcrResultError,						// Ocr识别结果错误
        /* Info Msg: about Task */
        TaskStart = 1000,					// 任务开始
        TaskMatched,						// 任务匹配成功
        ReachedLimit,						// 单个原子任务达到次数上限
        ReadyToSleep,						// 准备开始睡眠
        EndOfSleep,							// 睡眠结束
        AppendProcessTask,					// 新增流程任务，Assistance内部消息，外部不需要处理
        AppendTask,							// 新增任务，Assistance内部消息，外部不需要处理
        TaskCompleted,						// 单个原子任务完成
        PrintWindow,						// 截图消息
        ProcessTaskStopAction,				// 流程任务执行到了Stop的动作
        TaskChainCompleted,					// 任务链完成
        ProcessTaskNotMatched,				// 流程任务识别错误
        AllTasksCompleted,                  // 所有任务完成
        TaskChainStart,                     // 开始任务链
        /* Info Msg: about Identify */
        TextDetected = 2000,				// 识别到文字
        ImageFindResult,					// 查找图像的结果
        ImageMatched,						// 图像匹配成功
        StageDrops,                         // 关卡掉落信息
        /* Open Recruit Msg */
        RecruitTagsDetected = 3000,			// 公招识别到了Tags
        RecruitSpecialTag,					// 公招识别到了特殊的Tag
        RecruitResult,						// 公开招募结果
        /* Infrast Msg */
        InfrastSkillsDetected = 4000,  // 识别到了基建技能（当前页面）
        InfrastSkillsResult,           // 识别到的所有可用技能
        InfrastComb,                   // 当前房间的最优干员组合
        EnterFacility,                 // 进入某个房间
        FacilityInfo,                  // 当前设施信息
    };

    public enum UsesOfDrones
    {
        DronesNotUse = 0,
        DronesTrade = 0x100,
        DronesTradeMoney = DronesTrade & 0x10,
        DronesMfg = 0x200,
        DronesMfgCombatRecord = DronesMfg | 0x10,
        DronesMfgPureGold = DronesMfg | 0x20
    };

    public enum InfrastWorkMode
    {
        Invaild = -1,
        Gentle,         // 温和换班模式：会对干员人数不满的设施进行换班，计算单设施内最优解，尽量不破坏原有的干员组合；即若设施内干员是满的，则不对该设施进行换班
        Aggressive,     // 激进换班模式：会对每一个设施进行换班，计算单设施内最优解，但不会将其他设施中的干员替换过来；即按工作状态排序，仅选择前面的干员
        Extreme         // 偏激换班模式：会对每一个设施进行换班，计算全局的单设施内最优解，为追求更高效率，会将其他设施内的干员也替换过来；即按技能排序，计算所有拥有该设施技能的干员效率，无论他在不在其他地方工作
    };
}
