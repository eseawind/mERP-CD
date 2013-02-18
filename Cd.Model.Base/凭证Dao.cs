﻿using System;
using System.Collections.Generic;
using System.Text;
using Hd.Model;
using Feng;

namespace Cd.Model
{
    public class 凭证Dao : BaseSubmittedDao<凭证>, ICancellateDao
    {
        internal class RepCon : IRepositoryConsumer
        {
            public string RepositoryCfgName
            {
                get { return "hd.model.cn.config"; }
                set { }
            }
        }
        private static RepCon s_repCon = new RepCon();

        public override IRepository GenerateRepository()
        {
            return ServiceProvider.GetService<IRepositoryFactory>().GenerateRepository(s_repCon);
        }


       /// <summary>
       /// 提交
       /// </summary>
       /// <param name="rep"></param>
       /// <param name="entity"></param>
        public override void Submit(IRepository rep, 凭证 entity)
        {
            bool sh = entity.审核状态;
            bool sz = entity.收支状态;
            bool sub = entity.Submitted;

            if (entity.是否作废)
            {
                throw new InvalidUserOperationException("该凭证已作废");
            }
            if ((entity.凭证费用明细 == null || entity.凭证费用明细.Count == 0) &&
                (entity.凭证收支明细 == null || entity.凭证收支明细.Count == 0))
            {
                throw new InvalidUserOperationException("请先填写明细！");
            }

            try
            {
                if (Feng.Authority.AuthorizeByRule("R:会计"))
                {
                    if (entity.凭证费用明细 != null && entity.凭证费用明细.Count > 0)
                    {                        
                        Audit(rep, entity);
                        entity.Submitted = true;
                    }
                }

                if (Feng.Authority.AuthorizeByRule("R:出纳"))
                {
                    if (entity.凭证收支明细 != null && entity.凭证收支明细.Count > 0)
                    {
                        ConfirmSzzt(rep, entity);
                        entity.Submitted = true;
                    }
                }

                this.Update(rep, entity);
            }
            catch (Exception)
            {
                entity.审核状态 = sh;
                entity.收支状态 = sz;
                entity.Submitted = sub;
                throw;
            }
        }

        /// <summary>
        /// 复核提交
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void SubmitAudit(IRepository rep, 凭证 entity)
        {
            entity.审核人编号 = Feng.SystemConfiguration.UserName;
            this.Update(rep, entity);
        }

        /// <summary>
        /// 复核撤销
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void UnSubmitAudit(IRepository rep, 凭证 entity)
        {
            entity.审核人编号 = null;
            this.Update(rep, entity);
        }

        public void Submit1(IRepository rep, 凭证 entity)
        {
            bool sh = entity.审核状态;
            bool sz = entity.收支状态;
            bool sub = entity.Submitted;

            if (entity.是否作废)
            {
                throw new InvalidUserOperationException("该凭证已作废");
            }
            if ((entity.凭证费用明细 == null || entity.凭证费用明细.Count == 0) &&
                (entity.凭证收支明细 == null || entity.凭证收支明细.Count == 0))
            {
                throw new InvalidUserOperationException("请先填写明细！");
            }

            try
            {
                if (Feng.Authority.AuthorizeByRule("R:会计"))
                {
                    if (entity.凭证费用明细 != null && entity.凭证费用明细.Count > 0)
                    {
                        Audit(rep, entity);
                        entity.Submitted = true;
                    }
                }

                if (Feng.Authority.AuthorizeByRule("R:出纳"))
                {
                    if (entity.凭证收支明细 != null && entity.凭证收支明细.Count > 0)
                    {
                        ConfirmSzzt(rep, entity);
                        entity.Submitted = true;
                    }
                }

                this.Update(rep, entity);
            }
            catch (Exception)
            {
                entity.审核状态 = sh;
                entity.收支状态 = sz;
                entity.Submitted = sub;
                throw;
            }
        }

        /// <summary>
        /// 判断凭证费用明细 凭证收支明细  金额是否一致
        /// </summary>
        /// <param name="entity"></param>
        public void MoneyIsSame(凭证 entity)
        {
            if (entity.金额.币制编号 == "CNY")
            {
                if (entity.金额.数额 != entity.会计金额)
                {
                    throw new InvalidUserOperationException("会计金额和出纳金额不付，请重新填写！");
                }
            }
            else
            {
                if (entity.金额.数额.Value == 0)
                {
                    if (entity.会计金额.Value == 0)
                    {
                    }
                    else
                    {
                        throw new InvalidUserOperationException("出纳金额不对，请重新填写！");
                    }
                }
                else
                {
                    string msg = "当前汇率为" + entity.会计金额.Value / entity.金额.数额.Value + "，是否正确？";

                    if (!ServiceProvider.GetService<IMessageBox>().ShowYesNo(msg))
                    {
                        throw new InvalidUserOperationException("出纳金额不对，请重新填写！");
                    }
                }
            }
        }

        /// <summary>
        /// 撤销提交
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public override void Unsubmit(IRepository rep, 凭证 entity)
        {
            throw new InvalidUserOperationException("凭证只能作废不能撤销提交！");
        }

        /// <summary>
        /// 作废。
        /// 审核状态 = true，此时反审核。
        /// 收支状态 = true，此时反支付确认
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void Cancellate(IRepository rep, object entity)
        {
            凭证 pz = entity as 凭证;

            try
            {
                if (pz.审核状态)
                {
                    Unaudit(rep, pz);
                    pz.审核状态 = false;
                }
                else if (pz.收支状态)
                {
                    UnConfirmSzzt(rep, pz);
                    pz.收支状态 = false;
                }
                pz.Submitted = false;
                pz.是否作废 = true;

                // 无论收付，都要撤销费用
                foreach (凭证费用明细 pzs1 in pz.凭证费用明细)
                {
                    rep.Initialize(pzs1.费用, pzs1);
                    foreach (费用 fee in pzs1.费用)
                    {
                        fee.凭证费用明细 = null;
                        (new 费用Dao()).Update(rep, fee);
                    }

                    (new HdBaseDao<凭证费用明细>()).Update(rep, pzs1);
                }

                this.Update(rep, pz);
            }
            catch (Exception)
            {
                pz.是否作废 = false;
            }
        }

        /// <summary>
        /// 收支确认    带rep的Transaction在外面
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void ConfirmSzzt(IRepository rep, 凭证 entity)
        {
            try
            {
                //if (entity.凭证类别 == 凭证类别.收款凭证)
                //{
                //    throw new InvalidUserOperationException("收款凭证不需要收支确认!");
                //}
                if (entity.收支状态)
                {
                    //throw new InvalidUserOperationException("已经通过出纳收支确认!");
                    return;
                }

                rep.Initialize(entity.凭证收支明细, entity);
                if (entity.凭证收支明细 != null && entity.凭证收支明细.Count > 0)
                {
                    decimal sum = 0;
                    foreach (凭证收支明细 item in entity.凭证收支明细)
                    {
                        if (item.收付标志 == 收付标志.收)
                            sum -= item.金额.Value;
                        else if (item.收付标志 == 收付标志.付)
                            sum += item.金额.Value;
                        else
                            throw new InvalidUserOperationException("未填写有效的收付标志！");
                    }
                    decimal rsum = entity.凭证类别 == 凭证类别.付款凭证 ? sum : -sum;
                    if (entity.金额.数额 != rsum)
                    {
                        throw new InvalidUserOperationException("凭证收支明细总额和凭证金额不符！");
                    }

                    //if (entity.审核状态)
                    //{
                    //    MoneyIsSame(entity);
                    //}

                    entity.收支状态 = true;
                    entity.出纳编号 = SystemConfiguration.UserName;

                    foreach (凭证收支明细 i in entity.凭证收支明细)
                    {
                        凭证收支明细Dao.支付(rep, i);
                    }
                }
                else
                {
                    throw new InvalidUserOperationException("必须填写凭证收支明细！");
                    //entity.收支状态 = false;
                }

                this.Update(rep, entity);
            }
            catch (Exception)
            {
                entity.收支状态 = false;
                throw;
            }
        }

        /// <summary>
        /// 收支确认撤销
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void UnConfirmSzzt(IRepository rep, 凭证 entity)
        {
            try
            {
                //if (entity.凭证类别 == 凭证类别.收款凭证)
                //{
                //    throw new InvalidUserOperationException("收款凭证不需要撤销收支确认!");
                //}
                if (!entity.收支状态)
                {
                    //throw new InvalidUserOperationException("还未通过收支确认，不需要撤销!");
                    // 当作废时
                    return;
                }

                entity.收支状态 = false;
                rep.Initialize(entity.凭证收支明细, entity);
                foreach (凭证收支明细 i in entity.凭证收支明细)
                {
                    凭证收支明细Dao.取消支付(rep, i);
                }

                if (!entity.审核状态)
                {
                    entity.Submitted = false;
                }

                this.Update(rep, entity);
            }
            catch (Exception)
            {
                entity.收支状态 = true;
                throw;
            }
        }

        /// <summary>
        /// 审核
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void Audit(IRepository rep, 凭证 entity)
        {
            
                //if (entity.凭证类别 == 凭证类别.付款凭证)
                //{
                //    throw new InvalidUserOperationException("付款凭证不需要审核!");
                //}
                if (entity.审核状态)
                {
                    //throw new InvalidUserOperationException("已经通过会计审核!");
                    return;
                }

                decimal? temp会计金额 = entity.会计金额;
                try
                {
                    rep.Initialize(entity.凭证费用明细, entity);
                    decimal sum = 0;
                    if (entity.凭证费用明细 != null && entity.凭证费用明细.Count > 0)
                    {
                        foreach (凭证费用明细 item in entity.凭证费用明细)
                        {
                            if (item.收付标志 == 收付标志.收)
                                sum -= item.金额.Value;
                            else if (item.收付标志 == 收付标志.付)
                                sum += item.金额.Value;
                            else
                                throw new InvalidUserOperationException("未填写有效的收付标志！");
                        }

                        entity.会计金额 = entity.凭证类别 == 凭证类别.付款凭证 ? sum : -sum;
                        //if (entity.会计金额 != entity.金额.数额)
                        //{
                        //    throw new InvalidUserOperationException("凭证费用明细总额和凭证金额不符！");
                        //}

                        //if (entity.收支状态)
                        //{
                            MoneyIsSame(entity);
                        //}

                        entity.审核状态 = true;
                        entity.会计编号 = SystemConfiguration.UserName;
                        GeneratePzYsyf(rep, entity);
                    }
                    else
                    {
                        entity.审核状态 = false;
                        throw new InvalidUserOperationException("必须填写凭证费用明细！");
                    }

                    this.Update(rep, entity);
                }
                catch (Exception)
                {
                    entity.审核状态 = false;
                    entity.会计金额 = temp会计金额;
                    throw;
                }
        }

        /// <summary>
        /// 审核撤销
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="entity"></param>
        public void Unaudit(IRepository rep, 凭证 entity)
        {
            try
            {
                //if (entity.凭证类别 == 凭证类别.付款凭证)
                //{
                //    throw new InvalidUserOperationException("付款凭证不需要撤销审核!");
                //}
                if (!entity.审核状态)
                {
                    //throw new InvalidUserOperationException("还未通过审核，不需要撤销!");
                    return;
                }

                rep.Initialize(entity.凭证费用明细, entity);
                entity.审核状态 = false;
                UngeneratePzYsyf(rep, entity);

                if (!entity.收支状态)
                {
                    entity.Submitted = false;
                }

                this.Update(rep, entity);
            }
            catch (Exception)
            {
                entity.审核状态 = true;
                throw;
            }
        }

        private static string s_非业务默认费用实体 = ServiceProvider.GetService<IDefinition>().TryGetValue("非业务默认费用实体");
        private static void GeneratePzYsyf(IRepository rep, 凭证 pz)
        {
            rep.Initialize(pz.凭证费用明细, pz);

            foreach (凭证费用明细 pzfymx in pz.凭证费用明细)
            {
                rep.Initialize(pzfymx.费用, pzfymx);

                费用项 fyx = EntityBufferCollection.Instance.Get<费用项>(pzfymx.费用项编号);
                if (pzfymx.费用.Count > 0)
                {
                    // 通过对账单包进来的
                    if (fyx.应收应付类型 == 应收应付类型.业务)
                    {
                        应收应付款 ysyfk = new 应收应付款();
                        ysyfk.IsActive = true;
                        ysyfk.费用项编号 = pzfymx.费用项编号;
                        ysyfk.结算期限 = pz.日期;
                        ysyfk.金额 = -pzfymx.金额;      // 取反
                        ysyfk.日期 = pz.日期;
                        ysyfk.收付标志 = pzfymx.收付标志;
                        ysyfk.相关人编号 = pzfymx.相关人编号;
                        ysyfk.业务类型编号 = pzfymx.业务类型编号.Value;
                        ysyfk.应收应付源 = pz;
                        ysyfk.备注 = pzfymx.备注;

                        (new HdBaseDao<应收应付款>()).Save(rep, ysyfk);
                    }
                    continue;
                }


                if (fyx.应收应付类型 == 应收应付类型.业务)
                {
                    应收应付款 ysyfk = new 应收应付款();
                    ysyfk.IsActive = true;
                    ysyfk.费用项编号 = pzfymx.费用项编号;
                    ysyfk.结算期限 = pz.日期;
                    ysyfk.金额 = -pzfymx.金额;      // 取反
                    ysyfk.日期 = pz.日期;
                    ysyfk.收付标志 = pzfymx.收付标志;
                    ysyfk.相关人编号 = pzfymx.相关人编号;
                    ysyfk.业务类型编号 = pzfymx.业务类型编号.Value;
                    ysyfk.应收应付源 = pz;
                    ysyfk.备注 = pzfymx.备注;

                    (new HdBaseDao<应收应付款>()).Save(rep, ysyfk);
                }
                else if (fyx.应收应付类型 == 应收应付类型.借款类型)
                {
                    应收应付款 ysyfk = new 应收应付款();
                    ysyfk.IsActive = true;
                    ysyfk.费用项编号 = "002";   // 其他

                    //IList<GridColumnInfo> infos = new List<GridColumnInfo> { 
                    //        new GridColumnInfo { Caption = "结算期限", DataControlVisible = "True", DataControlType = "MyDatePicker", PropertyName = "结算期限", NotNull = "True"} ,
                    //    };
                    //ArchiveDataControlForm form = new ArchiveDataControlForm(new ControlManager(null), infos);
                    //if (form.ShowDialog() == DialogResult.OK)
                    //{
                    //    ysyfk.结算期限 = (DateTime)form.DataControls["结算期限"].SelectedDataValue;
                    //}
                    //else
                    //{
                    //    throw new InvalidUserOperationException("借款必须填写结算期限！");
                    //}
                    ysyfk.结算期限 = pzfymx.结算期限.HasValue ? pzfymx.结算期限.Value : pz.日期;
                    ysyfk.金额 = pzfymx.金额;
                    ysyfk.日期 = pz.日期;
                    ysyfk.收付标志 = pzfymx.收付标志 == 收付标志.付 ? 收付标志.收 : 收付标志.付;
                    ysyfk.相关人编号 = pzfymx.相关人编号;
                    ysyfk.业务类型编号 = pzfymx.业务类型编号.Value;
                    ysyfk.应收应付源 = pz;
                    ysyfk.备注 = pzfymx.备注;

                    (new HdBaseDao<应收应付款>()).Save(rep, ysyfk);
                }
                else if (fyx.应收应付类型 == 应收应付类型.管理费用类型)
                {
                    非业务费用 fy = new 非业务费用();
                    fy.IsActive = true;

                    fy.费用项编号 = pzfymx.费用项编号;
                    fy.金额 = pzfymx.金额;
                    fy.收付标志 = pzfymx.收付标志;
                    fy.相关人编号 = pzfymx.相关人编号;
                    fy.备注 = pzfymx.备注;

                    if (string.IsNullOrEmpty(s_非业务默认费用实体))
                    {
                        throw new ArgumentException("必须指定一个非业务默认费用实体!");
                    }
                    fy.费用实体 = rep.Get<费用实体>(new Guid(s_非业务默认费用实体));
                    if (fy.费用实体 == null)
                    {
                        throw new ArgumentException("指定的非业务默认费用实体不存在!");
                    }
                    fy.凭证费用明细 = pzfymx;

                    (new 非业务费用Dao()).Save(rep, fy);
                }
                else if (fyx.应收应付类型 == 应收应付类型.待分摊类型)
                {
                    应收应付款 ysyfk = new 应收应付款();
                    ysyfk.IsActive = true;
                    ysyfk.费用项编号 = "004";   // 其他
                    ysyfk.结算期限 = pzfymx.结算期限.HasValue ? pzfymx.结算期限.Value : pz.日期;
                    ysyfk.金额 = pzfymx.金额;
                    ysyfk.日期 = pz.日期;
                    ysyfk.收付标志 = pzfymx.收付标志 == 收付标志.付 ? 收付标志.收 : 收付标志.付;
                    ysyfk.相关人编号 = "900031";
                    ysyfk.业务类型编号 = pzfymx.业务类型编号.Value;
                    ysyfk.应收应付源 = pz;
                    ysyfk.备注 = pzfymx.备注;

                    (new HdBaseDao<应收应付款>()).Save(rep, ysyfk);
                }
                else if (fyx.应收应付类型 == 应收应付类型.其他)
                {
                }
            }
        }

        private static void UngeneratePzYsyf(IRepository rep, 凭证 pz)
        {
            IList<应收应付款> ysyfk = (rep as Feng.NH.INHibernateRepository).List<应收应付款>(NHibernate.Criterion.DetachedCriteria.For<应收应付款>()
                              .Add(NHibernate.Criterion.Expression.Eq("应收应付源", pz)));

            foreach (应收应付款 i in ysyfk)
            {
                rep.Delete(i);
            }

            rep.Initialize(pz.凭证费用明细, pz);

            foreach (凭证费用明细 pzfymx in pz.凭证费用明细)
            {
                费用项 fyx = EntityBufferCollection.Instance.Get<费用项>(pzfymx.费用项编号);

                rep.Initialize(pzfymx.费用, pzfymx);
                if (pzfymx.费用.Count > 0 &&
                    fyx.应收应付类型 != 应收应付类型.管理费用类型)
                {
                    continue;
                }

                if (fyx.应收应付类型 == 应收应付类型.业务)
                {
                }
                else if (fyx.应收应付类型 == 应收应付类型.借款类型)
                {
                }
                else if (fyx.应收应付类型 == 应收应付类型.管理费用类型)
                {
                    if (string.IsNullOrEmpty(s_非业务默认费用实体))
                    {
                        throw new ArgumentException("必须指定一个非业务默认费用实体!");
                    }

                    IList<费用> fys = new List<费用>();
                    foreach (费用 fy in pzfymx.费用)
                    {
                        if (fy.费用实体.ID == new Guid(s_非业务默认费用实体))
                        {
                            fys.Add(fy);
                        }
                    }

                    foreach (费用 fy in fys)
                    {
                        pzfymx.费用.Remove(fy);
                        rep.Delete(fy);
                    }
                }
                else if (fyx.应收应付类型 == 应收应付类型.其他)
                {
                }
            }
        }
    }
}