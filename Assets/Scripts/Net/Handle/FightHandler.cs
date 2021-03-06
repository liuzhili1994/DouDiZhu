﻿using System.Collections.Generic;
using Protocol.Code;
using Protocol.Dto.Card;
using Protocol.Dto.Constant;
using Protocol.Dto.Fight;
using UnityEngine;

public class FightHandler : HandlerBase
{
    private UIMsg uiMsg = new UIMsg();
    private AudioMsg audioMsg = new AudioMsg();
    public override void OnReceive(int subCode, object value)
    {
        switch (subCode)
        {
            case FightCode.BAOJING_SRES:
                {
                    if ((int)value == 1)//报警就剩一张牌了
                    {
                        audioMsg.Set("Sound/Fight/","baojing1");
                        Dispatch(AreaCode.AUDIO,AudioEvent.EFFECTAUDIO, audioMsg);
                        Dispatch(AreaCode.AUDIO, AudioEvent.BGMAUDIO, 2);
                    }
                    else if ((int)value == 2)//报警就剩两张牌了
                    {
                        audioMsg.Set("Sound/Fight/", "baojing2");
                        Dispatch(AreaCode.AUDIO, AudioEvent.EFFECTAUDIO, audioMsg);
                        Dispatch(AreaCode.AUDIO, AudioEvent.BGMAUDIO, 2);
                    }
                }
                break;
            case FightCode.BACKTOFIGHT_SRES:
                BackToFight();
                break;
            case FightCode.RESTART:
                Restart();
                break;
            case FightCode.GET_CARDS_SRES:
                GetCardsResponse(value as List<CardDto>);
                break;
            case FightCode.QIANG_LANDLORD_BRO://点击抢地主按钮 的回应 发来一个landlordDto 包含userId 和 底牌
                QiangLandlordBro(value as LandlordDto);
                break;
            
            case FightCode.CHUPAI_SRES:
                {
                    uiMsg.Set("管不上", Color.red);
                    Dispatch(AreaCode.UI, UIEvent.MessageInfoPanel, uiMsg);
                }
                break;
            //case FightCode.TURN_SRES:

            //    break;
            case FightCode.QIANG_TURN_BRO://该谁抢地主
                TurnToQiang((int)value);
                break;
            case FightCode.CHUPAI_TURN_BRO://该谁出牌
                TurnToChu((int)value);
                break;

            case FightCode.CHUPAI_BRO:
                ChuPaiResponse(value as ChuPaiDto);
                break;
            case FightCode.BUCHU_BRO:
                BuChuResponse((int)value);
                break;
            case FightCode.BUCHU_SRES:
                {
                    uiMsg.Set("不能不出，你是头家", Color.red);
                    Dispatch(AreaCode.UI, UIEvent.MessageInfoPanel, uiMsg);
                }
                break;
            case FightCode.BUQIANG_LANDLORD_BRO://谁不抢
                BuQiangLandlordResponse((int)value);
                break;
            case FightCode.LEAVE_BRO://离开了要干嘛？把ui隐藏了
                {
                    var userId = (int)value;
                    if (Models.gameModel.MatchRoomDto != null)
                    {
                        //移除之前先把信息保存下来
                        var leaveUserDto = Models.gameModel.MatchRoomDto.uIdUdtoDic[userId];
                        //移除数据
                        Models.gameModel.MatchRoomDto.Delete(userId);
                        //玩家离开了房间  把准备文字和对话面板  地主或农民的ui隐藏
                        Dispatch(AreaCode.UI, UIEvent.PLAYER_LEAVE, userId);
                        //提示消息  xxx 离开了房间
                        uiMsg.Set(string.Format("玩家 ：{0}  退出", leaveUserDto.name), Color.green);
                        Dispatch(AreaCode.UI, UIEvent.MessageInfoPanel, uiMsg);
                    }
                }
                break;
            case FightCode.OVER_BRO:
                GameOver(value as OverDto);
                break;
            default:
                break;
        }
    }

    private void BackToFight()
    {
        //先把overPanel隐藏了
        Dispatch(AreaCode.UI,UIEvent.BACKTOFIGHT,null);
        ////重新开始  跟抢地主一样  instead of 直接回到匹配场景
        //Restart();
        LoadSceneMsg msg = new LoadSceneMsg(1, () => {
            Debug.Log("返回匹配界面。。。");
            //当前还没匹配。所以匹配数据要为空
            Models.gameModel.MatchRoomDto = null;
            //向服务器发送请求获取角色信息
            var serverMsg = new MessageData();
            serverMsg.Set(OpCode.USER, UserCode.GET_INFO_CREQ, null);
            Dispatch(AreaCode.NET, 0, serverMsg);
        });
        Dispatch(AreaCode.SCENE,SceneEvent.LOAD_SCENE, msg);
    }

    private void GameOver(OverDto dto)
    {
        Dispatch(AreaCode.UI, UIEvent.GameOver, dto);
        
    }

    /// <summary>
    /// 没人抢地主重新开始
    /// </summary>
    private void Restart()
    {
        //将ui隐藏
        Dispatch(AreaCode.UI,UIEvent.GAME_RESTAET,null);
        //将牌全部隐藏了
        Dispatch(AreaCode.CHARACTER,CharactorEvent.GMAE_RESTART,null);
    }


    /// <summary>
    /// 抢地主响应
    /// </summary>
    /// <param name="dto"></param>
    private void QiangLandlordBro(LandlordDto dto)
    {
        //给设置身份
        Dispatch(AreaCode.UI,UIEvent.PLAYER_CHANGE_IDENTITY,dto.landLordId);
        //所有人显示显示底牌
        Dispatch(AreaCode.UI,UIEvent.SET_TABLE_CARDS,dto.tableCardsList);
        //给地主添加底牌
        Dispatch(AreaCode.CHARACTER,CharactorEvent.SET_LANDLORD_TABLECARDS,dto);
        //播放抢地主的声音
        //Dispatch(AreaCode.AUDIO,AudioEvent.EFFECTAUDIO,);
        //显示抢地主操作结果。就像如果xx不抢，服务器会广播xx不抢，自己要把不抢的结果txt显示出来，其他玩家也要显示谁不抢的txt
        Dispatch(AreaCode.UI,UIEvent.QIANG_LANDLORD_OPERATE,dto.landLordId);

        //播放抢地主音效
        audioMsg.Set("Sound/Fight/", "Order");
        Dispatch(AreaCode.AUDIO, AudioEvent.EFFECTAUDIO, audioMsg);
    }


    /// <summary>
    /// 给每个玩家发牌
    /// </summary>
    /// <param name="cards"></param>
    private void GetCardsResponse(List<CardDto> cards)
    {
        //改变倍数为1
        Dispatch(AreaCode.UI,UIEvent.CHANGE_MUTIPLIER,1);
        //让玩家隐藏readyTxt
        Dispatch(AreaCode.UI,UIEvent.PLAYER_HIDE_STATE,null);
        //根据服务器发来的牌，生成预设物体
        Dispatch(AreaCode.CHARACTER,CharactorEvent.SET_MYPLAYER_CARDS,cards);
        Dispatch(AreaCode.CHARACTER,CharactorEvent.SET_LEFTPLAYER_CARDS,null);
        Dispatch(AreaCode.CHARACTER,CharactorEvent.SET_RIGHTLAYER_CARDS,null);

        //播放战斗音乐
        Dispatch(AreaCode.AUDIO, AudioEvent.BGMAUDIO, 1);
        

    }

    private void BuQiangLandlordResponse(int userId)
    {
        Dispatch(AreaCode.UI,UIEvent.BUQIANG_LANDLORD_OPERATE,userId);
        audioMsg.Set("Sound/Fight/", "NoOrder");
        Dispatch(AreaCode.AUDIO,AudioEvent.EFFECTAUDIO,audioMsg);
    }

    private void BuChuResponse(int userId)
    {
        Dispatch(AreaCode.UI, UIEvent.BUCHU_OPERATE, userId);

        audioMsg.Set("Sound/Fight/", "buyao"+ Random.Range(1, 5));
        Dispatch(AreaCode.AUDIO, AudioEvent.EFFECTAUDIO, audioMsg);
    }

    private void ChuPaiResponse(ChuPaiDto dto)
    {
        //给玩家发消息，你出牌了 可以把牌移除了
        Dispatch(AreaCode.CHARACTER,CharactorEvent.CHUPAI_SRES,dto);
        //告诉UI 把牌显示出来
        Dispatch(AreaCode.UI,UIEvent.CHUPAI_OPERATE,dto);
        //播放语音出了什么牌
        audioMsg.Set("Sound/Fight/", GetAudioName(dto));
        Dispatch(AreaCode.AUDIO,AudioEvent.EFFECTAUDIO,audioMsg);
    }

    private void TurnToQiang(int userId)
    {
        Dispatch(AreaCode.UI,UIEvent.SHOW_PLAYER_JIAO_BTN_ACTIVE,userId);
    }

    private void TurnToChu(int userId)
    {
        Dispatch(AreaCode.UI, UIEvent.SHOW_PLAYER_CHUPAI_BTN_ACTIVE, userId);
    }

    private string GetAudioName(ChuPaiDto dto)
    {
        string outStr = string.Empty;

        if (dto.type == CardsType.Boom || dto.type == CardsType.Joker_Boom)
        {
            return dto.type.ToString();
        }
        

        if (!dto.isBiggest)
        {
            //不是最大者出的牌 直接返回 大你说这压死
            var rNum = Random.Range(1,4);
            return "dani" + rNum;
        }

        //判断牌型
        if (dto.type == CardsType.Single)
        {
            outStr += (int)dto.weight + 3;
        }
        else if (dto.type == CardsType.Double)
        {
            outStr += "dui" + ((int)dto.weight + 3);
        }
        else if (dto.type == CardsType.Straight)
        {
            outStr += "shunzi";
        }
        else if (dto.type == CardsType.Double_Straight)
        {
            outStr += "liandui";
        }
        else if (dto.type == CardsType.Three_Straight || dto.type == CardsType.Three_Straight_One || dto.type == CardsType.Three_Straight_Two)
        {
            outStr += "feiji";
        }
        else if (dto.type == CardsType.Three)
        {
            outStr += "tuple" + ((int)dto.weight + 3);
        }
        else if (dto.type == CardsType.Three_One)
        {
            outStr += "sandaiyi";
        }
        else if (dto.type == CardsType.Three_Two)
        {
            outStr += "sandaiyidui";
        }

        return outStr;
    }
}
