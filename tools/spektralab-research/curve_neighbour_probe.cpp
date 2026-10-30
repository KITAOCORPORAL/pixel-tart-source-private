// Research driver only: calls the pinned engine's real profile loader, table builder,
// curve shader and CDF model evaluator. No engine implementation is copied here.
#include "core/profile.hpp"
#include "core/curves.hpp"
#include "gpu/gpu.hpp"
#include "gpu_vulkan_read.hpp"
#include <fstream>
#include <memory>
#include <cstdio>
#include <cmath>
#include <iterator>
using spk::Json;
Json array(const std::vector<double>& v){Json a=Json::array();for(double x:v)a.push(Json(x));return a;}
Json floats(const std::vector<float>& v){Json a=Json::array();for(float x:v)a.push(Json(double(x)));return a;}
int main(int argc,char**argv){
 if(argc!=6){std::fprintf(stderr,"resources stock queries.f32 output.json gamma\n");return 2;}
 std::string error;spk::Profile film;if(!spk::load_profile(argv[1],argv[2],film,error)){puts(error.c_str());return 3;}
 std::ifstream f(argv[3],std::ios::binary);std::vector<char> bytes((std::istreambuf_iterator<char>(f)),{});
 if(bytes.empty()||bytes.size()%12)return 4;std::vector<float> queries(bytes.size()/4);std::memcpy(queries.data(),bytes.data(),bytes.size());
 auto gpu=std::unique_ptr<spk::gpu::Gpu>(spk::gpu::Gpu::create_vulkan(std::string(argv[1])+"/vulkan",error));if(!gpu){puts(error.c_str());return 5;}
 const double gamma[]={std::stod(argv[5]),std::stod(argv[5]),std::stod(argv[5])};spk::InterpTables tables;spk::build_interp_tables(film.data.log_exposure,gamma,film.normalized_curves,tables);
 std::vector<float> x(tables.x.begin(),tables.x.end()),y(tables.y.begin(),tables.y.end()),inv(tables.inv.begin(),tables.inv.end()),actual;
 auto input=gpu->upload(queries.data(),bytes.size(),error),xb=gpu->upload(x.data(),x.size()*4,error),yb=gpu->upload(y.data(),y.size()*4,error),ib=gpu->upload(inv.data(),inv.size()*4,error),out=gpu->alloc(bytes.size(),error);
 uint32_t meta[]={uint32_t(tables.k),uint32_t(queries.size()/3)};
 if(!gpu->dispatch("spk_curves",{spk::gpu::Arg::buf(input),spk::gpu::Arg::buf(xb),spk::gpu::Arg::buf(ib),spk::gpu::Arg::buf(yb),spk::gpu::Arg::inline_bytes(meta,2),spk::gpu::Arg::buf(out)},meta[1],error)||!gpu->flush(error)||!read_test_buffer(gpu.get(),out,actual,error)){puts(error.c_str());return 6;}
 spk::Vec model;spk::PrintCurvesMorphParams none;
 if(!spk::print_curves_morph(film.data.log_exposure,film.data.model,none,film.info.is_positive(),model,error)){puts(error.c_str());return 7;}
 Json report=Json::object();report.set("device",Json(gpu->device_name()));report.set("rawX",array(film.data.log_exposure));report.set("rawY",array(film.data.density_curves));report.set("normalisedY",array(film.normalized_curves));report.set("x32",floats(x));report.set("y32",floats(y));report.set("inv32",floats(inv));report.set("queries",floats(queries));report.set("actual",floats(actual));report.set("modelCdfDiagnosticOnly",array(model));
 std::ofstream output(argv[4]);output<<report.dump()<<"\n";
}
