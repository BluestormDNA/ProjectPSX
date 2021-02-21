using System;

namespace ProjectPSX.Disassembler {
    internal class BIOS_Disassembler {

        private BUS bus;
        private string msg = "";

        public BIOS_Disassembler(BUS bus) {
            this.bus = bus;
        }

        internal void verbose(uint PC, uint[] GPR) {
            uint pc = PC & 0x1fffffff;
            uint function = GPR[9];
            uint arg1 = GPR[4];
            uint arg2 = GPR[5];
            uint arg3 = GPR[6];
            uint arg4 = GPR[7];

            switch (pc) {
                case 0xA0:
                    msg = $"[BIOS] [Function A {function:x2}] ";
                    switch (function) {
                        case 0x00: //msg += $"FileOpen({biosOutput(arg1)}, {biosOutput(arg2)})"; break;
                        case 0x01: msg += $"FileSeek(fd, offset, seektype)"; break;
                        case 0x02: msg += $"FileRead(fd, dst, length)"; break;
                        case 0x03: msg += $"FileWrite(fd, src, length)"; break;
                        case 0x04: msg += $"FileClose(fd)"; break;
                        case 0x05: msg += $"FileIoctl(fd, cmd, arg)"; break;
                        case 0x06: msg += $"exit(exitcode)"; break;
                        case 0x07: msg += $"FileGetDeviceFlag(fd)"; break;
                        case 0x08: msg += $"FileGetc(fd)"; break;
                        case 0x09: msg += $"FilePutc(char, fd)"; break;
                        case 0x0A: msg += $"todigit(char)"; break;
                        case 0x0B: msg += $"atof(src); Does NOT work - uses(ABSENT) cop1!!!"; break;
                        case 0x0C: msg += $"strtoul(src, src_end, base)"; break;
                        case 0x0D: msg += $"strtol(src, src_end, base)"; break;
                        case 0x0E: msg += $"abs(val)"; break;
                        case 0x0F: msg += $"labs(val)"; break;

                        case 0x10: msg += $"atoi(src)"; break;
                        case 0x11: msg += $"atol(src)"; break;
                        case 0x12: msg += $"atob(src,num_dst)"; break;
                        case 0x13: msg += $"SaveState(buf)"; break;
                        case 0x14: msg += $"RestoreState(buf,param)"; break;
                        case 0x15: msg += $"strcat({biosOutput(arg1)},{biosOutput(arg2)})"; break;
                        case 0x16: msg += $"strncat({biosOutput(arg1)},{biosOutput(arg2)},{arg3})"; break;
                        case 0x17: msg += $"strcmp({biosOutput(arg1)},{biosOutput(arg2)})"; break;
                        case 0x18: msg += $"strncmp({biosOutput(arg1)},{biosOutput(arg2)},{arg3})"; break;
                        case 0x19: msg += $"strcpy({biosOutput(arg1)},{biosOutput(arg2)})"; break;
                        case 0x1A: msg += $"strncpy({biosOutput(arg1)},{biosOutput(arg2)},{arg3})"; break;
                        case 0x1B: msg += $"strlen({biosOutput(arg1)})"; break;
                        case 0x1C: msg += $"index(src,char)"; break;
                        case 0x1D: msg += $"rindex(src,char)"; break;
                        case 0x1E: msg += $"strchr(src,char"; break;
                        case 0x1F: msg += $" strrchr(src,char)"; break;

                        case 0x20: msg += $"strpbrk(src,list)"; break;
                        case 0x21: msg += $"strspn(src,list)"; break;
                        case 0x22: msg += $"strcspn(src,list)"; break;
                        case 0x23: msg += $"strtok(src,list)"; break;
                        case 0x24: msg += $"strstr(str,substr)"; break;
                        case 0x25: msg += $"toupper(char)"; break;
                        case 0x26: msg += $"tolower(char)"; break;
                        case 0x27: msg += $"bcopy(src,dst,len)"; break;
                        case 0x28: msg += $"bzero(dst = {arg1:x8},len = {arg2:x8})"; break;
                        case 0x29: msg += $"bcmp(ptr1,ptr2,len"; break;
                        case 0x2A: msg += $"memcpy(dst,src,len)"; break;
                        case 0x2B: msg += $"memset(dst,fillbyte,len)"; break;
                        case 0x2C: msg += $"memmove(dst,src,len)"; break;
                        case 0x2D: msg += $"memcmp(src1,src2,len)"; break;
                        case 0x2E: msg += $"memchr(src,scanbyte,len)"; break;
                        case 0x2F: msg += $"rand()"; break;

                        case 0x30: msg += $"srand(seed)"; break;
                        case 0x31: msg += $"qsort(base,nel,width,callback)"; break;
                        case 0x32: msg += $"strtod(src,src_end)"; break;
                        case 0x33: msg += $"malloc(size)"; break;
                        case 0x34: msg += $"free(buf)"; break;
                        case 0x35: msg += $"lsearch(key,base,nel,width,callback)"; break;
                        case 0x36: msg += $"bsearch(key,base,nel,width,callback)"; break;
                        case 0x37: msg += $"calloc(sizx,sizy)"; break;
                        case 0x38: msg += $"realloc(old_buf,new_siz)"; break;
                        case 0x39: msg += $"InitHeap(addr,size)"; break;
                        case 0x3A: msg += $"SystemErrorExit(exitcode)"; break;
                        case 0x3B: msg += $"std_in_getchar()"; break;
                        case 0x3C: msg += $"std_out_putchar(char)"; break;
                        case 0x3D: msg += $"std_in_gets(dst)"; break;
                        case 0x3E: msg += $"std_out_puts(src)"; break;
                        case 0x3F: msg += $"printf(txt,param1,param2,etc.)"; break;

                        case 0x40: msg += $"SystemErrorUnresolvedException()"; break;
                        case 0x41: msg += $"LoadExeHeader(filename,headerbuf)"; break;
                        case 0x42: msg += $"LoadExeFile(filename,headerbuf)"; break;
                        case 0x43: msg += $"DoExecute(headerbuf,param1,param2)"; break;
                        case 0x44: msg += $"FlushCache()"; break;
                        case 0x45: msg += $"init_a0_b0_c0_vectors"; break;
                        case 0x46: msg += $"GPU_dw(Xdst,Ydst,Xsiz,Ysiz,src)"; break;
                        case 0x47: msg += $"gpu_send_dma(Xdst,Ydst,Xsiz,Ysiz,src)"; break;
                        case 0x48: msg += $"SendGP1Command(gp1cmd)"; break;
                        case 0x49: msg += $"GPU_cw(gp0cmd)"; break;
                        case 0x4A: msg += $"GPU_cwp(src,num)"; break;
                        case 0x4B: msg += $"send_gpu_linked_list(src)"; break;
                        case 0x4C: msg += $"gpu_abort_dma()"; break;
                        case 0x4D: msg += $"GetGPUStatus()"; break;
                        case 0x4E: msg += $"gpu_sync()"; break;
                        case 0x4F: //0x50
                        case 0x50: msg += $"SystemError"; break;

                        case 0x51: msg += $"LoadAndExecute(filename,stackbase,stackoffset)"; break;
                        case 0x52: msg += $"SystemError ----OR---- GetSysSp()"; break;
                        case 0x53: msg += $"SystemError           ;PS2: set_ioabort_handler(src)"; break;
                        case 0x54: msg += $"CdInit()"; break;
                        case 0x55: msg += $"_bu_init()"; break;
                        case 0x56: msg += $"CdRemove()"; break;
                        case 0x57:
                        case 0x58:
                        case 0x59:
                        case 0x5A: msg += $"return 0"; break;
                        case 0x5B: msg += $"dev_tty_init()  "; break;
                        case 0x5C: msg += $"dev_tty_open(fcb,and unused:'path\name',accessmode)"; break;
                        case 0x5D: msg += $"dev_tty_in_out(fcb,cmd) "; break;
                        case 0x5E: msg += $"dev_tty_ioctl(fcb,cmd,arg)"; break;
                        case 0x5F: msg += $"dev_cd_open(fcb,'path\name',accessmode)"; break;

                        case 0x60: msg += $"dev_cd_read(fcb,dst,len)"; break;
                        case 0x61: msg += $"dev_cd_close(fcb)"; break;
                        case 0x62: msg += $"dev_cd_firstfile(fcb,'path\name',direntry)"; break;
                        case 0x63: msg += $"dev_cd_nextfile(fcb,direntry)"; break;
                        case 0x64: msg += $"dev_cd_chdir(fcb,'path')"; break;
                        case 0x65: msg += $"dev_card_open(fcb,'path\name',accessmode)"; break;
                        case 0x66: msg += $"dev_card_read(fcb,dst,len)"; break;
                        case 0x67: msg += $"dev_card_write(fcb,src,len)"; break;
                        case 0x68: msg += $"dev_card_close(fcb)"; break;
                        case 0x69: msg += $"dev_card_firstfile(fcb,'path\name',direntry)"; break;
                        case 0x6A: msg += $"dev_card_nextfile(fcb,direntry)"; break;
                        case 0x6B: msg += $"dev_card_erase(fcb,'path\name')"; break;
                        case 0x6C: msg += $"dev_card_undelete(fcb,'path\name')"; break;
                        case 0x6D: msg += $"dev_card_format(fcb)"; break;
                        case 0x6E: msg += $"dev_card_rename(fcb1,'path\name1',fcb2,'path\name2')"; break;
                        case 0x6F: msg += $"?   ;card ;[r4+18h]=00000000h  ;card_clear_error(fcb) or so"; break;

                        case 0x70: msg += $"_bu_init()"; break;
                        case 0x71: msg += $"CdInit()"; break;
                        case 0x72: msg += $"CdRemove()"; break;
                        case 0x73:
                        case 0x74:
                        case 0x75:
                        case 0x76:
                        case 0x77: msg += $"return 0"; break;
                        case 0x78: msg += $"CdAsyncSeekL({arg1:x8})"; break;
                        case 0x79:
                        case 0x7A:
                        case 0x7B: msg += $"return 0"; break;
                        case 0x7C: msg += $"CdAsyncGetStatus({arg1:x8})"; break;
                        case 0x7D: msg += $"return 0"; break;
                        case 0x7E: msg += $"CdAsyncReadSector({arg1:x8)},{arg2:x8},{arg3:x8})"; break;
                        case 0x7F:

                        case 0x80: msg += $"return 0"; break;
                        case 0x81: msg += $"CdAsyncSetMode({arg1})"; break;
                        case 0x82:
                        case 0x83:
                        case 0x84:
                        case 0x85:
                        case 0x86:
                        case 0x87:
                        case 0x88:
                        case 0x89:
                        case 0x8A:
                        case 0x8B:
                        case 0x8C:
                        case 0x8D:
                        case 0x8E:
                        case 0x8F: msg += $"return 0"; break;

                        case 0x90: msg += $"CdromIoIrqFunc1()"; break;
                        case 0x91: msg += $"CdromDmaIrqFunc1()"; break;
                        case 0x92: msg += $"CdromIoIrqFunc2()"; break;
                        case 0x93: msg += $"CdromDmaIrqFunc2()"; break;
                        case 0x94: msg += $"CdromGetInt5errCode(dst1,dst2)"; break;
                        case 0x95: msg += $"CdInitSubFunc()"; break;
                        case 0x96: msg += $"AddCDROMDevice()"; break;
                        case 0x97: msg += $"AddMemCardDevice()"; break;
                        case 0x98: msg += $"AddDuartTtyDevice()"; break;
                        case 0x99: msg += $"AddDummyTtyDevice("; break;
                        case 0x9A:
                        case 0x9B: msg += $"SystemError"; break;
                        case 0x9C: msg += $"SetConf(num_EvCB,num_TCB,stacktop)"; break;
                        case 0x9D: msg += $"GetConf(num_EvCB_dst,num_TCB_dst,stacktop_dst)"; break;
                        case 0x9E: msg += $"SetCdromIrqAutoAbort(type, flag)"; break;
                        case 0x9F: msg += $"SetMemSize(megabytes)"; break;

                        case 0xA0: msg += $"WarmBoot()"; break;
                        case 0xA1: msg += $"SystemErrorBootOrDiskFailure({(char)arg1},{arg2:x8})"; break;
                        case 0xA2: msg += $"EnqueueCdIntr()"; break;
                        case 0xA3: msg += $"DequeueCdIntr()"; break;
                        case 0xA4: msg += $"CdGetLbn(filename)"; break;
                        case 0xA5: msg += $"CdReadSector(count,sector,buffer)"; break;
                        case 0xA6: msg += $"CdGetStatus()"; break;
                        case 0xA7: msg += $"bu_callback_okay()"; break;
                        case 0xA8: msg += $"bu_callback_err_write()"; break;
                        case 0xA9: msg += $"bu_callback_err_busy()"; break;
                        case 0xAA: msg += $"bu_callback_err_eject()"; break;
                        case 0xAB: msg += $"_card_info(port)"; break;
                        case 0xAC: msg += $"_card_async_load_directory(port)"; break;
                        case 0xAD: msg += $"set_card_auto_format(flag)"; break;
                        case 0xAE: msg += $"bu_callback_err_prev_write()"; break;
                        case 0xAF: msg += $"card_write_test(port)"; break;

                        case 0xB0:
                        case 0xB1: msg += $"return 0"; break;
                        case 0xB2: msg += $"ioabort_raw(param)"; break;
                        case 0xB3: msg += $"return 0"; break;
                        case 0xB4: msg += $"GetSystemInfo(index)"; break;
                        case uint _ when pc >= 0xB5 && pc <= 0xBF:
                            msg += $"jump_to_00000000h"; break;
                    }
                    log(msg);
                    break;
                case 0xB0:
                    msg = $"[BIOS] [Function B {function:x2}] ";
                    switch (function) {
                        case 0x00: msg += "alloc_kernel_memory(size)"; break;
                        case 0x01: msg += "free_kernel_memory(buf)"; break;
                        case 0x02: msg += "init_timer(t,reload,flags)"; break;
                        case 0x03: msg += "get_timer(t)"; break;
                        case 0x04: msg += "enable_timer_irq(t)"; break;
                        case 0x05: msg += "disable_timer_irq(t)"; break;
                        case 0x06: msg += "restart_timer(t)"; break;
                        case 0x07: msg += $"DeliverEvent(class = {arg1:x8}, spec = {arg2:x8})"; break;
                        case 0x08: msg += $"OpenEvent(class = {arg1:x8},spec = {arg2:x8},mode = {arg3:x8},func = {arg4:x8})"; break;
                        case 0x09: msg += $"CloseEvent(event = {arg1:x8})"; break;
                        case 0x0A: msg += $"WaitEvent(event = {arg1:x8})"; break;
                        case 0x0B: return; //msg += $"TestEvent({arg1:x8})"; break; //SPAM
                        case 0x0C: msg += $"EnableEvent(event = {arg1:x8})"; break;
                        case 0x0D: msg += $"DisableEvent(event = {arg1:x8})"; break;
                        case 0x0E: msg += "OpenThread(reg_PC,reg_SP_FP,reg_GP)"; break;
                        case 0x0F: msg += "CloseThread(handle)"; break;

                        case 0x10: msg += $"ChangeThread(handle)"; break;
                        case 0x11: msg += $"jump_to_00000000h"; break;
                        case 0x12: msg += $"InitPad(buf1,siz1,buf2,siz2)"; break;
                        case 0x13: msg += $"StartPad()"; break;
                        case 0x14: msg += $"StopPad()"; break;
                        case 0x15: msg += $"OutdatedPadInitAndStart(type,button_dest,unused,unused)"; break;
                        case 0x16: msg += $"OutdatedPadGetButtons()"; break;
                        case 0x17: return; // msg += "ReturnFromException()"; break; //SPAM
                        case 0x18: msg += $"SetDefaultExitFromException()"; break;
                        case 0x19: msg += $"SetCustomExitFromException(addr)"; break;
                        case 0x1A:
                        case 0x1B:
                        case 0x1C:
                        case 0x1D:
                        case 0x1E:
                        case 0x1F: msg += $"SystemError  ;PS2: return 0"; break;

                        case 0x20: msg += $"UnDeliverEvent({arg1.ToString("x8")},{arg2.ToString("x8")})"; break;
                        case 0x21:
                        case 0x22:
                        case 0x23: msg += $"SystemError  ;PS2: return 0"; break;
                        case 0x24:
                        case 0x25:
                        case 0x26:
                        case 0x27:
                        case 0x28:
                        case 0x29: msg += $"jump_to_00000000h"; break;
                        case 0x2A:
                        case 0x2B: msg += $"SystemError  ;PS2: return 0"; break;
                        case 0x2C:
                        case 0x2D:
                        case 0x2E:
                        case 0x2F:

                        case 0x30:
                        case 0x31: msg += $"jump_to_00000000h"; break;
                        case 0x32: msg += $"FileOpen({arg1.ToString("x8")},{arg2}) {biosOutput(arg1)}"; break;
                        case 0x33: msg += $"FileSeek(fd,offset,seektype)"; break;
                        case 0x34: msg += $"FileRead(fd,dst,length)"; break;
                        case 0x35: msg += $"FileWrite(fd,src,length)"; break;
                        case 0x36: msg += $"FileClose(fd)"; break;
                        case 0x37: msg += $"FileIoctl(fd,cmd,arg)"; break;
                        case 0x38: msg += $"exit(exitcode)"; break;
                        case 0x39: msg += $"FileGetDeviceFlag(fd)"; break;
                        case 0x3A: msg += $"FileGetc(fd)"; break;
                        case 0x3B: msg += $"FilePutc(char,fd)"; break;
                        case 0x3C: msg += $"std_in_getchar()"; break;
                        case 0x3D: return; //msg += $"std_out_putchar({(char)arg1})"; break; //No need for this with TTY
                        case 0x3E: msg += $"std_in_gets(dst)"; break;
                        case 0x3F: msg += $"std_out_puts(src)"; break;

                        case 0x40: msg += $"chdir(name)"; break;
                        case 0x41: msg += $"FormatDevice(devicename)"; break;
                        case 0x42: msg += $"firstfile(filename,direntry)"; break;
                        case 0x43: msg += $"nextfile(direntry)"; break;
                        case 0x44: msg += $"FileRename(old_filename,new_filename)"; break;
                        case 0x45: msg += $"FileDelete(filename)"; break;
                        case 0x46: msg += $"FileUndelete(filename)"; break;
                        case 0x47: msg += $"AddDevice(device_info)"; break;
                        case 0x48: msg += $"RemoveDevice(device_name_lowercase)"; break;
                        case 0x49: msg += $"PrintInstalledDevices()"; break;
                        case 0x4A: msg += $"InitCard(pad_enable)"; break;
                        case 0x4B: msg += $"StartCard()"; break;
                        case 0x4C: msg += $"StopCard()"; break;
                        case 0x4D: msg += $"_card_info_subfunc(port)"; break;
                        case 0x4E: msg += $"write_card_sector(port,sector,src)"; break;
                        case 0x4F: msg += $"read_card_sector(port,sector,dst)"; break;

                        case 0x50: msg += $"allow_new_card()"; break;
                        case 0x51: msg += $"Krom2RawAdd(shiftjis_code)"; break;
                        case 0x52: msg += $"SystemError  ;PS2: return 0"; break;
                        case 0x53: msg += $"Krom2Offset(shiftjis_code)"; break;
                        case 0x54: msg += $"GetLastError()"; break;
                        case 0x55: msg += $"GetLastFileError(fd)"; break;
                        case 0x56: msg += $"GetC0Table"; break;
                        case 0x57: msg += $"GetB0Table"; break;
                        case 0x58: msg += $"get_bu_callback_port()"; break;
                        case 0x59: msg += $"testdevice(devicename)"; break;
                        case 0x5A: msg += $"SystemError  ;PS2: return 0"; break;
                        case 0x5B: msg += $"ChangeClearPad(int)"; break;
                        case 0x5C: msg += $"get_card_status(slot)"; break;
                        case 0x5D: msg += $"wait_card_status(slot)"; break;
                        case uint _ when pc >= 0x5E && pc <= 0xFF:
                            msg += $"jump_to_00000000h"; break;
                    }
                    log(msg);
                    break;
                case 0xC0:
                    msg = $"[BIOS] [Function C {function:x2}] ";
                    switch (function) {
                        case 0x00: msg += $"EnqueueTimerAndVblankIrqs(priority) ;used with prio=1"; break;
                        case 0x01: msg += $"EnqueueSyscallHandler(priority)     ;used with prio=0"; break;
                        case 0x02: msg += $"SysEnqIntRP(priority,struc)  ;bugged, use with care"; break;
                        case 0x03: msg += $"SysDeqIntRP(priority,struc)  ;bugged, use with care"; break;
                        case 0x04: msg += $"get_free_EvCB_slot()"; break;
                        case 0x05: msg += $"get_free_TCB_slot()"; break;
                        case 0x06: msg += $"ExceptionHandler()"; break;
                        case 0x07: msg += $"InstallExceptionHandlers()  ;destroys/uses k0/k1"; break;
                        case 0x08: msg += $"SysInitMemory(addr,size)"; break;
                        case 0x09: msg += $"SysInitKernelVariables()"; break;
                        case 0x0A: msg += $"ChangeClearRCnt(t,flag)"; break;
                        case 0x0B: msg += $"SystemError  ;PS2: return 0"; break;
                        case 0x0C: msg += $"InitDefInt(priority) ;used with prio=3"; break;
                        case 0x0D: msg += $"SetIrqAutoAck(irq,flag)"; break;
                        case 0x0E: msg += $"return 0               ;DTL-H2000: dev_sio_init"; break;
                        case 0x0F: msg += $"return 0               ;DTL-H2000: dev_sio_open"; break;

                        case 0x10: msg += $"return 0               ;DTL-H2000: dev_sio_in_out"; break;
                        case 0x11: msg += $"return 0               ;DTL-H2000: dev_sio_ioctl"; break;
                        case 0x12: msg += $"InstallDevices(ttyflag)"; break;
                        case 0x13: msg += $"FlushStdInOutPut()"; break;
                        case 0x14: msg += $"return 0               ;DTL-H2000: SystemError"; break;
                        case 0x15: msg += $"tty_cdevinput(circ,char)"; break;
                        case 0x16: msg += $" tty_cdevscan()"; break;
                        case 0x17: msg += $"tty_circgetc(circ)    ;uses r5 as garbage txt for ioabort"; break;
                        case 0x18: msg += $"tty_circputc(char,circ)"; break;
                        case 0x19: msg += $"ioabort(txt1,txt2)"; break;
                        case 0x1A: msg += $"set_card_find_mode(mode)  ;0=normal, 1=find deleted files"; break;
                        case 0x1B: msg += $"KernelRedirect(ttyflag)   ;PS2: ttyflag=1 causes SystemError"; break;
                        case 0x1C: msg += $"AdjustA0Table()"; break;
                        case 0x1D: msg += $"get_card_find_mode()"; break;
                        case uint _ when pc >= 0x1E && pc <= 0x7E:
                            msg += $"jump_to_00000000h"; break;
                        case uint _ when pc >= 0x80 && pc <= 0xFF:
                            msg += $"MIRROR Function launching B " + (function - 0x80).ToString("x2");
                            GPR[9] = GPR[9] - 0x80;
                            verbose(0xB0, GPR);
                            break;
                    }
                    log(msg);
                    break;
            }
        }

        private void log(string msg) {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        private string biosOutput(uint addr) {
            string output = "";
            char c = (char)bus.load32(addr++);
            while (c != '\0') {
                output += c;
                c = (char)bus.load32(addr++);
            }
            return output;
            //return null;
        }
    }
}
